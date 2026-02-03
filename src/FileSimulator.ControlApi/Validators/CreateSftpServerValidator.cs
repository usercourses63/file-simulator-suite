using FluentValidation;
using FileSimulator.ControlApi.Models;

namespace FileSimulator.ControlApi.Validators;

public class CreateSftpServerValidator : AbstractValidator<CreateSftpServerRequest>
{
    public CreateSftpServerValidator()
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

        RuleFor(x => x.Uid)
            .InclusiveBetween(1, 65534)
            .WithMessage("UID must be between 1 and 65534");

        RuleFor(x => x.Gid)
            .InclusiveBetween(1, 65534)
            .WithMessage("GID must be between 1 and 65534");
    }
}
