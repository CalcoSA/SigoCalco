using SigoCalco.Authentication.Domain.Dtos;
using FluentValidation;

namespace SigoCalco.Authentication.Domain.Validators
{
    public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
    {
        public CreateUserDtoValidator()
        {
            RuleFor(x => x.WordpressUserId)
                .GreaterThan(0UL)
                .WithMessage("El WordpressUserId debe ser mayor a cero.");

            RuleFor(x => x.UserLogin)
                .NotEmpty()
                .WithMessage("El UserLogin es obligatorio.")
                .MaximumLength(60)
                .WithMessage("El UserLogin no puede superar 60 caracteres.");

            RuleFor(x => x.UserName)
                .NotEmpty()
                .WithMessage("El UserName es obligatorio.")
                .MaximumLength(250)
                .WithMessage("El UserName no puede superar 250 caracteres.");

            RuleFor(x => x.Email)
                .Must(UserEmail.IsValid)
                .When(x => x.Email is not null)
                .WithMessage("Debe proporcionar un correo electrónico válido de hasta 254 caracteres.");

            RuleFor(x => x.SolutionCenterId)
                .NotNull().WithMessage("Debe seleccionar un Centro de Soluciones.")
                .GreaterThan(0).WithMessage("El identificador del Centro de Soluciones debe ser mayor a cero.");

            RuleFor(x => x.IdRole)
                .GreaterThan(0)
                .WithMessage("El IdRole debe ser mayor a cero.");
        }
    }
}