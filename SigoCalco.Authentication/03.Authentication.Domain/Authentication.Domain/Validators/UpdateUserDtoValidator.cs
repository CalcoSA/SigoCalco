using SigoCalco.Authentication.Domain.Dtos;
using FluentValidation;

namespace SigoCalco.Authentication.Domain.Validators
{
    public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserDtoValidator()
        {
            RuleFor(x => x.IdUser)
                .GreaterThan(0)
                .WithMessage("El IdUser debe ser mayor a cero.");

            RuleFor(x => x.IdRole)
                .GreaterThan(0).When(x => x.IdRole.HasValue)
                .WithMessage("El IdRole debe ser mayor a cero.");

            RuleFor(x => x.SolutionCenterId)
                .GreaterThan(0).When(x => x.SolutionCenterId.HasValue)
                .WithMessage("El identificador del Centro de Soluciones debe ser mayor a cero.");

            RuleFor(x => x.Email)
                .Must(UserEmail.IsValid).When(x => x.Email is not null)
                .WithMessage("Debe proporcionar un correo electrónico válido de hasta 254 caracteres.");
        }
    }
}