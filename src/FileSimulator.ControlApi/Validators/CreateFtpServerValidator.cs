using FluentValidation;
using FileSimulator.ControlApi.Services;

namespace FileSimulator.ControlApi.Validators;

public class CreateFtpServerValidator : AbstractValidator<CreateFtpServerRequest>
{
    public CreateFtpServerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .Matches("^[a-z0-9-]+$").WithMessage("Name must be lowercase alphanumeric with hyphens only")
            .Length(3, 32).WithMessage("Name must be between 3 and 32 characters");

        RuleFor(x => x.NodePort)
            .InclusiveBetween(30000, 32767)
            .When(x => x.NodePort.HasValue)
            .WithMessage("NodePort must be in range 30000-32767");

        RuleFor(x => x.Username)
            .NotEmpty()
            .Length(3, 32);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");

        RuleFor(x => x.PassivePortStart)
            .InclusiveBetween(30000, 32700)
            .When(x => x.PassivePortStart.HasValue)
            .WithMessage("PassivePortStart must be in range 30000-32700");

        RuleFor(x => x.PassivePortEnd)
            .GreaterThan(x => x.PassivePortStart ?? 0)
            .When(x => x.PassivePortEnd.HasValue && x.PassivePortStart.HasValue)
            .WithMessage("PassivePortEnd must be greater than PassivePortStart");
    }
}
