using Microsoft.Extensions.DependencyInjection;
using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Application.Services;
using SigoCalco.Authentication.Domain.Validators;
using SigoCalco.Authentication.Domain.Dtos;
using FluentValidation;

namespace SigoCalco.Authentication.Application
{
    public static class ApplicationExtensions
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IMenuOptionApplication, MenuOptionApplication>();
            services.AddScoped<IRoleApplication, RoleApplication>();
            services.AddScoped<IUserApplication, UserApplication>();
            services.AddScoped<ISolutionCenterApplication, SolutionCenterApplication>();
            services.AddScoped<IJwtTokenApplication, JwtTokenApplication>();
            services.AddScoped<ISSOSignatureApplication, SSOSignatureApplication>();
            services.AddScoped<IAuthApplication, AuthApplication>();
            services.AddScoped<IWordpressUserApplication, WordpressUserApplication>();
            services.AddScoped<ILogApplication, LogApplication>();

            services.AddScoped<IValidator<CreateRoleDto>, CreateRoleDtoValidator>();
            services.AddScoped<IValidator<UpdateRoleDto>, UpdateRoleDtoValidator>();
            services.AddScoped<IValidator<CreateUserDto>, CreateUserDtoValidator>();
            services.AddScoped<IValidator<UpdateUserDto>, UpdateUserDtoValidator>();

            return services;
        }
    }
}
