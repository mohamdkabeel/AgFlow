namespace AgFlow.Application.Common.Interfaces;

public interface IPortalPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public record PortalAccessToken(string Token, int ExpiresInSeconds);

// توكن مختلف تماماً عن توكن الفريق: مفتاح توقيع مختلف + Audience مختلف.
// حتى لو Endpoint كتبناه بالغلط [Authorize] بس، توكن العميل مش بيعدي (التوقيع مش متطابق مع الـ Scheme الافتراضي)
public interface IPortalTokenService
{
    PortalAccessToken GenerateAccessToken(Guid contactId, Guid workspaceId, Guid clientId);
}

public record PortalIdentity(Guid ContactId, Guid ClientId, Guid WorkspaceId);

// مصدر الحقيقة لـ "مين العميل اللي بيطلب". ClientId بييجي من هنا (الـ Token) أبداً من الـ Request
public interface ICurrentPortalUser
{
    PortalIdentity? Identity { get; }
}
