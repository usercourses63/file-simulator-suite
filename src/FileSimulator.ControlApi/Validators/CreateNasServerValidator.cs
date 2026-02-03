using FluentValidation;
using FileSimulator.ControlApi.Services;

namespace FileSimulator.ControlApi.Validators;

public class CreateNasServerValidator : AbstractValidator<CreateNasServerRequest>
{
    public CreateNasServerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .Matches("^[a-z0-9-]+$").WithMessage("Name must be lowercase alphanumeric with hyphens only")
            .Length(3, 32).WithMessage("Name must be between 3 and 32 characters");

        RuleFor(x => x.NodePort)
            .InclusiveBetween(30000, 32767)
            .When(x => x.NodePort.HasValue)
            .WithMessage("NodePort must be in range 30000-32767");

        RuleFor(x => x.Directory)
            .NotEmpty()
            .Must(dir => !dir.Contains("..")).WithMessage("Directory must not contain '..' path traversal")
            .Must(dir => !dir.StartsWith("/")).WithMessage("Directory must be relative (not start with '/')")
            .MaximumLength(256).WithMessage("Directory path must not exceed 256 characters");

        RuleFor(x => x.ExportOptions)
            .NotEmpty()
            .MaximumLength(512).WithMessage("ExportOptions must not exceed 512 characters");
    }
}
