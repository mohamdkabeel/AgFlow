using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Common;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Modules.Invitations.Commands.AcceptInvitation;

// FullName/Password مطلوبين بس لو صاحب الإيميل مالوش حساب. لو عنده حساب، الباسوورد بيتتجاهل تماماً
public record AcceptInvitationCommand(string Token, string? FullName, string? Password)
    : IRequest<AcceptInvitationResult>;

public record AcceptInvitationResult(Guid WorkspaceId);

public class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FullName).MaximumLength(200);
        RuleFor(x => x.Password).MaximumLength(128); // حد أقصى عشان الـ Hashing البطيء مايبقاش وسيلة DoS
    }
}

public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, AcceptInvitationResult>
{
    private const string InvalidMessage = "Invitation is invalid or has expired.";

    private readonly IApplicationDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly UserManager<User> _userManager;

    public AcceptInvitationCommandHandler(
        IApplicationDbContext dbContext, ITokenService tokenService, UserManager<User> userManager)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _userManager = userManager;
    }

    public async Task<AcceptInvitationResult> Handle(AcceptInvitationCommand request, CancellationToken ct)
    {
        // IgnoreQueryFilters موثّق: الطلب ده Anonymous (صاحب الدعوة لسه ماسجّلش دخول)، يعني مفيش WorkspaceId
        // في الـ Token والـ Filter كان هيرجع صفر. الأمان هنا من الـ Token العشوائي نفسه (512 bit، متخزن Hash)،
        // مش من الـ Filter. وكل فحص بعد كده بيستخدم WorkspaceId من الدعوة نفسها، مش من الـ Request.
        var hash = _tokenService.HashToken(request.Token);
        var invitation = await _dbContext.Invitations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == hash, ct);

        // رسالة واحدة لكل الحالات (مش موجودة / منتهية / اتقبلت / اتلغت): ماندّيش مهاجم معلومة عن الـ Token
        if (invitation is null || !invitation.IsUsable(DateTime.UtcNow))
            throw new ArgumentException(InvalidMessage);

        await _dbContext.BeginTransactionAsync();
        try
        {
            var user = await _userManager.FindByEmailAsync(invitation.Email);
            if (user is null)
            {
                if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrEmpty(request.Password))
                    throw new ArgumentException("Full name and password are required to create your account.");

                user = new User
                {
                    UserName = invitation.Email,
                    Email = invitation.Email,
                    FullName = request.FullName.Trim(),
                    IsActive = true,
                    // وصل للدعوة = بيقدر يقرا الإيميل ده، فده نفسه تأكيد الملكية
                    EmailConfirmed = true
                };
                var created = await _userManager.CreateAsync(user, request.Password);
                if (!created.Succeeded)
                    throw new ArgumentException(string.Join(" ", created.Errors.Select(e => e.Description)));
            }
            else if (!user.IsActive)
            {
                throw new ArgumentException(InvalidMessage);
            }

            // IgnoreQueryFilters لنفس السبب، مفلتر صراحةً بـ WorkspaceId بتاع الدعوة
            var membership = await _dbContext.WorkspaceMembers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.WorkspaceId == invitation.WorkspaceId && m.UserId == user.Id, ct);

            if (membership is null)
                _dbContext.WorkspaceMembers.Add(WorkspaceMember.CreateFromInvitation(
                    invitation.WorkspaceId, user.Id, invitation.Role, invitation.HourlyCost));
            else if (membership.Status == MembershipStatus.Deactivated)
                membership.Reactivate(invitation.Role, invitation.HourlyCost);
            else
                throw new DomainException("You are already an active member of this workspace.");

            invitation.Accept();
            await _dbContext.SaveChangesAsync(ct);
            await _dbContext.CommitTransactionAsync();

            return new AcceptInvitationResult(invitation.WorkspaceId);
        }
        catch (DbUpdateException)
        {
            // قبول متزامن لنفس الدعوة (طلبين مع بعض): الـ Unique Index منع التاني
            await _dbContext.RollbackTransactionAsync();
            throw new ArgumentException(InvalidMessage);
        }
        catch
        {
            await _dbContext.RollbackTransactionAsync();
            throw;
        }
    }
}
