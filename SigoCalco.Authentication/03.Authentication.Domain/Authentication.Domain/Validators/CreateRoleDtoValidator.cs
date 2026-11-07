using SigoCalco.Authentication.Domain.Dtos;
using FluentValidation;

namespace SigoCalco.Authentication.Domain.Validators
{
    public class CreateRoleDtoValidator : AbstractValidator<CreateRoleDto>
    {
        public CreateRoleDtoValidator()
        {
            RuleFor(x => x.NameRole)
                .NotEmpty().WithMessage("El nombre del rol es obligatorio.")
                .MaximumLength(100).WithMessage("El nombre del rol no puede superar 100 caracteres.");

            RuleFor(x => x.StatusRole)
                .Must(x => x == 0 || x == 1)
                .WithMessage("El estado del rol debe ser 0 o 1.");

            RuleFor(x => x.MenuOptionIds)
                .NotNull().WithMessage("Debe enviar las opciones de menú.")
                .Must(x => x.Any()).WithMessage("Debe asignar al menos una opción de menú.")
                .Must(x => x.All(id => id > 0)).WithMessage("Todas las opciones de menú deben ser mayores a cero.");
        }
    }
}