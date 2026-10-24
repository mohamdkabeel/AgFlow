namespace AgFlow.Application.Common.Exceptions;

// الـ Workspace في وضع القراءة فقط (انتهى الـ Trial أو الاشتراك اتلغى). بتتحول لـ 402 Payment Required
public class SubscriptionReadOnlyException : Exception
{
    public SubscriptionReadOnlyException() : base("Workspace is in read-only mode.") { }
}
