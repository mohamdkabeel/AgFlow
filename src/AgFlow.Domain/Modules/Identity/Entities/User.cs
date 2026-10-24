using Microsoft.AspNetCore.Identity;

namespace AgFlow.Domain.Modules.Identity.Entities;

// IdentityUser<Guid> بيدينا: Id, Email, PasswordHash, UserName...
// إحنا بس بنضيف الحقول الخاصة بينا فوقها
public class User : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // FR-TT-3: التكلفة الأساسية للساعة — بتتقرأ منها الـ Snapshot وقت تسجيل الوقت،
    // لكن هي نفسها ممكن تتغير مستقبلاً من غير ما تأثر على السجلات القديمة
    public decimal HourlyCost { get; set; }
}
