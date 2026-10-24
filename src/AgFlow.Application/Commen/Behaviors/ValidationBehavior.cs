using FluentValidation;
using MediatR;

namespace AgFlow.Application.Common.Behaviors;

// IPipelineBehavior بتتحط "حوالين" كل Command/Query بيتبعت عبر MediatR
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var failures = _validators
                .Select(v => v.Validate(context))
                .SelectMany(result => result.Errors)
                .Where(failure => failure is not null)
                .ToList();

            if (failures.Any())
                throw new ValidationException(failures);
        }

        // لو مفيش أخطاء، كمّل عادي للـ Handler الحقيقي (CreateClientCommandHandler)
        return await next();
    }
}
