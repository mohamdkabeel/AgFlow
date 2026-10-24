using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Projects.Entities;

namespace AgFlow.Application.Modules.Projects.Board;

// ============================================================================
// Phase: قراءة المشاريع والمهام + Task Lists + إنهاء/أرشفة المشروع (FR-PM-3, FR-PM-8)
// الأمان: كل Query بتبدأ من DbSet عليه الـ Global Tenant Filter (Projects/TaskLists/TaskItems).
// TaskAssignments مالهاش WorkspaceId، فمابنقراش منها غير بـ TaskItemIds طالعة من Query متفلترة.
// مفيش أي بيانات مالية هنا (سعر/تكلفة): القوايم دي بيشوفها كل الأدوار الداخلية.
// ============================================================================

// ---------- Task List ----------
public record CreateTaskListCommand(Guid ProjectId, string Name) : IRequest<Guid>;

public class CreateTaskListCommandValidator : AbstractValidator<CreateTaskListCommand>
{
    public CreateTaskListCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class CreateTaskListCommandHandler : IRequestHandler<CreateTaskListCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    public CreateTaskListCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    { _db = db; _current = current; }

    public async Task<Guid> Handle(CreateTaskListCommand request, CancellationToken ct)
    {
        var workspaceId = _current.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        var project = await _db.Projects.AsNoTracking()
            .Where(p => p.Id == request.ProjectId)
            .Select(p => new { p.Status })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Project not found.");

        if (project.Status != ProjectStatus.Active)
            throw new AgFlow.Domain.Common.DomainException("Cannot add task lists to a project that is not active.");

        var list = TaskList.Create(workspaceId, request.ProjectId, request.Name.Trim());
        _db.TaskLists.Add(list);
        await _db.SaveChangesAsync(ct);
        return list.Id;
    }
}

// ---------- إنهاء / أرشفة ----------
public record ChangeProjectStatusCommand(Guid ProjectId, ProjectStatus Target) : IRequest;

public class ChangeProjectStatusCommandHandler : IRequestHandler<ChangeProjectStatusCommand>
{
    private readonly IApplicationDbContext _db;
    public ChangeProjectStatusCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(ChangeProjectStatusCommand request, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct)
            ?? throw new KeyNotFoundException("Project not found.");

        switch (request.Target)
        {
            case ProjectStatus.Completed: project.Complete(); break;
            case ProjectStatus.Archived: project.Archive(); break;
            default: throw new ArgumentException("Unsupported target status.");
        }
        await _db.SaveChangesAsync(ct);
    }
}

// ---------- قايمة المشاريع ----------
public record ProjectListItemDto(
    Guid Id, Guid ClientId, string Name, string Type, string Status, DateTime CreatedAt,
    int TotalTasks, int DoneTasks);

public record ListProjectsQuery(
    Guid? ClientId = null, ProjectStatus? Status = ProjectStatus.Active,
    int Page = 1, int PageSize = 25) : IRequest<IReadOnlyList<ProjectListItemDto>>;

public class ListProjectsQueryHandler : IRequestHandler<ListProjectsQuery, IReadOnlyList<ProjectListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListProjectsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProjectListItemDto>> Handle(ListProjectsQuery request, CancellationToken ct)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(request.Page, 1);

        var query = _db.Projects.AsNoTracking().AsQueryable();
        if (request.ClientId is Guid cid) query = query.Where(p => p.ClientId == cid);
        if (request.Status is { } st) query = query.Where(p => p.Status == st);

        var projects = await query
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id) // ترتيب ثابت للـ Pagination
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new { p.Id, p.ClientId, p.Name, p.Type, p.Status, p.CreatedAt })
            .ToListAsync(ct);

        var ids = projects.Select(p => p.Id).ToList();

        var counts = await _db.TaskItems.AsNoTracking()
            .Join(_db.TaskLists.Where(l => ids.Contains(l.ProjectId)),
                  t => t.TaskListId, l => l.Id, (t, l) => new { l.ProjectId, t.Status })
            .GroupBy(x => new { x.ProjectId, x.Status })
            .Select(g => new { g.Key.ProjectId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        return projects.Select(p =>
        {
            var mine = counts.Where(c => c.ProjectId == p.Id).ToList();
            return new ProjectListItemDto(p.Id, p.ClientId, p.Name, p.Type.ToString(), p.Status.ToString(),
                p.CreatedAt, mine.Sum(c => c.Count),
                mine.Where(c => c.Status == TaskItemStatus.Done).Sum(c => c.Count));
        }).ToList();
    }
}

// ---------- تفاصيل مشروع (Board) ----------
public record BoardTaskDto(
    Guid Id, string Title, string? Description, string Status, DateTime? DueDate,
    DateTime CreatedAt, IReadOnlyList<Guid> AssigneeUserIds);

public record BoardTaskListDto(Guid Id, string Name, IReadOnlyList<BoardTaskDto> Tasks);

public record ProjectDetailsDto(
    Guid Id, Guid ClientId, Guid? RetainerAgreementId, string Name, string Type, string Status,
    DateTime CreatedAt, IReadOnlyList<BoardTaskListDto> TaskLists);

public record GetProjectByIdQuery(Guid ProjectId) : IRequest<ProjectDetailsDto>;

public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, ProjectDetailsDto>
{
    private const int MaxTasks = 500; // سقف أمان: Board أكبر من كده يتقسم لصفحات (مستقبلاً)
    private readonly IApplicationDbContext _db;
    public GetProjectByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProjectDetailsDto> Handle(GetProjectByIdQuery request, CancellationToken ct)
    {
        var p = await _db.Projects.AsNoTracking()
            .Where(x => x.Id == request.ProjectId)
            .Select(x => new { x.Id, x.ClientId, x.RetainerAgreementId, x.Name, x.Type, x.Status, x.CreatedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Project not found.");

        var lists = await _db.TaskLists.AsNoTracking()
            .Where(l => l.ProjectId == p.Id)
            .OrderBy(l => l.Name).ThenBy(l => l.Id)
            .Select(l => new { l.Id, l.Name })
            .ToListAsync(ct);
        var listIds = lists.Select(l => l.Id).ToList();

        var tasks = await _db.TaskItems.AsNoTracking()
            .Where(t => listIds.Contains(t.TaskListId))
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Take(MaxTasks)
            .Select(t => new { t.Id, t.TaskListId, t.Title, t.Description, t.Status, t.DueDate, t.CreatedAt })
            .ToListAsync(ct);
        var taskIds = tasks.Select(t => t.Id).ToList();

        var assignments = await _db.TaskAssignments.AsNoTracking()
            .Where(a => taskIds.Contains(a.TaskItemId))
            .Select(a => new { a.TaskItemId, a.UserId })
            .ToListAsync(ct);
        var byTask = assignments.ToLookup(a => a.TaskItemId, a => a.UserId);

        var boardLists = lists.Select(l => new BoardTaskListDto(
            l.Id, l.Name,
            tasks.Where(t => t.TaskListId == l.Id)
                 .Select(t => new BoardTaskDto(t.Id, t.Title, t.Description, t.Status.ToString(),
                     t.DueDate, t.CreatedAt, byTask[t.Id].ToList()))
                 .ToList())).ToList();

        return new ProjectDetailsDto(p.Id, p.ClientId, p.RetainerAgreementId, p.Name,
            p.Type.ToString(), p.Status.ToString(), p.CreatedAt, boardLists);
    }
}

// ---------- قايمة المهام (فلاتر + "مهامي") ----------
public record TaskListItemDto(
    Guid Id, Guid TaskListId, Guid ProjectId, string ProjectName, string Title, string Status,
    DateTime? DueDate, DateTime CreatedAt, IReadOnlyList<Guid> AssigneeUserIds);

public record ListTasksQuery(
    Guid? ProjectId = null, Guid? TaskListId = null, TaskItemStatus? Status = null,
    bool AssignedToMe = false, int Page = 1, int PageSize = 25) : IRequest<IReadOnlyList<TaskListItemDto>>;

public class ListTasksQueryHandler : IRequestHandler<ListTasksQuery, IReadOnlyList<TaskListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    public ListTasksQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    { _db = db; _current = current; }

    public async Task<IReadOnlyList<TaskListItemDto>> Handle(ListTasksQuery request, CancellationToken ct)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(request.Page, 1);

        var tasks = _db.TaskItems.AsNoTracking().AsQueryable();
        if (request.TaskListId is Guid lid) tasks = tasks.Where(t => t.TaskListId == lid);
        if (request.Status is { } st) tasks = tasks.Where(t => t.Status == st);

        if (request.AssignedToMe)
        {
            var me = _current.UserId ?? throw new UnauthorizedAccessException("No user context found.");
            var myTaskIds = _db.TaskAssignments.Where(a => a.UserId == me).Select(a => a.TaskItemId);
            tasks = tasks.Where(t => myTaskIds.Contains(t.Id));
        }

        var rows = await tasks
            .Join(_db.TaskLists.AsNoTracking(), t => t.TaskListId, l => l.Id, (t, l) => new { t, l })
            .Join(_db.Projects.AsNoTracking(), x => x.l.ProjectId, p => p.Id, (x, p) => new { x.t, p })
            .Where(x => request.ProjectId == null || x.p.Id == request.ProjectId)
            .OrderBy(x => x.t.DueDate == null).ThenBy(x => x.t.DueDate).ThenBy(x => x.t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.t.Id, x.t.TaskListId, ProjectId = x.p.Id, ProjectName = x.p.Name,
                x.t.Title, x.t.Status, x.t.DueDate, x.t.CreatedAt
            })
            .ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToList();
        var assignments = await _db.TaskAssignments.AsNoTracking()
            .Where(a => ids.Contains(a.TaskItemId))
            .Select(a => new { a.TaskItemId, a.UserId })
            .ToListAsync(ct);
        var byTask = assignments.ToLookup(a => a.TaskItemId, a => a.UserId);

        return rows.Select(r => new TaskListItemDto(r.Id, r.TaskListId, r.ProjectId, r.ProjectName,
            r.Title, r.Status.ToString(), r.DueDate, r.CreatedAt, byTask[r.Id].ToList())).ToList();
    }
}
