namespace AgFlow.Domain.Common;

// خرق لقاعدة بزنس ("الفاتورة اتبعتت خلاص"، "الإدخال معتمد ومينفعش يتعدل").
// مختلفة عن InvalidOperationException اللي .NET بترميها لأخطاء برمجية حقيقية:
// الأولى = المستخدم عمل حاجة مش مسموحة (409)، التانية = عندنا Bug (500).
// لو خلطناهم، يا نخبي Bugs ورا 409، يا نرجّع 500 لمستخدم عمل حاجة طبيعية.
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
