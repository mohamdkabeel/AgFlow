using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Clients.Entities;

namespace AgFlow.Application.Modules.Portal;

// كل Query/Command في الـ Portal لازم يبدأ بالـ Guard ده. بيعمل حاجتين:
//  1) بيرجّع الـ ClientId من الـ Token (مش من الـ Request)
//  2) بيتأكد إن الحساب لسه مفعّل والعميل لسه نشط "دلوقتي". التوكن صالح 30 دقيقة،
//     ولو الموظف عطّل وصول الشخص ده، من غير الفحص ده كان يفضل شايف بيانات لحد ما التوكن ينتهي
internal static class PortalGuard
{
    public static async Task<PortalIdentity> RequireAsync(
        ICurrentPortalUser current, IApplicationDbContext db, CancellationToken ct)
    {
        var identity = current.Identity
            ?? throw new UnauthorizedAccessException("Portal authentication required.");

        var stillValid = await db.PortalCredentials.AsNoTracking().AnyAsync(c =>
            c.ClientContactId == identity.ContactId && c.ClientId == identity.ClientId && c.IsEnabled, ct)
            && await db.Clients.AsNoTracking().AnyAsync(c =>
                c.Id == identity.ClientId && c.Status == ClientStatus.Active, ct);

        if (!stillValid)
            throw new UnauthorizedAccessException("Portal access is no longer available.");

        return identity;
    }
}
