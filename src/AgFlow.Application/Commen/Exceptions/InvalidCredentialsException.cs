namespace AgFlow.Application.Common.Exceptions;

// رسالة واحدة عامة لكل أسباب فشل الدخول (إيميل غلط، باسوورد غلط، حساب معطل، Locked)
// عشان المهاجم ميقدرش يعرف أنهي إيميل مسجل عندنا (User Enumeration)
public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Invalid credentials.") { }
}
