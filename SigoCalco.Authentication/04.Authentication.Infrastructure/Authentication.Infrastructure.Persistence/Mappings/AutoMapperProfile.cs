using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Domain.Dtos;
using AutoMapper;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Mappings
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Menuoption, MenuOptionDto>();
            CreateMap<SolutionCenter, SolutionCenterDto>();
            CreateMap<MenuOptionDto, Menuoption>();
            CreateMap<Role, RoleDto>();
            CreateMap<RoleDto, Role>().ForMember(dest => dest.RoleCode, opt => opt.Ignore());
            CreateMap<Role, CreateRoleDto>();
            CreateMap<CreateRoleDto, Role>();
            CreateMap<Role, UpdateRoleDto>();
            CreateMap<UpdateRoleDto, Role>();
            CreateMap<UserDto, User>();
            CreateMap<User, CreateUserDto>();
            CreateMap<CreateUserDto, User>();
            CreateMap<User, UpdateUserDto>();
            CreateMap<UpdateUserDto, User>();
            CreateMap<Role, RoleDetailDto>()
                .ForMember(
                    dest => dest.MenuOptions,
                    opt => opt.MapFrom(src => src.Rolemenuoptions
                        .Where(x => x.IdMenuOptionNavigation.StatusMenuOption == 1)
                        .OrderBy(x => x.IdMenuOptionNavigation.OrderMenuOption)
                        .Select(x => x.IdMenuOptionNavigation)
                    )
                );
            CreateMap<User, UserDto>()
                .ForMember(
                    dest => dest.IdRole,
                    opt => opt.MapFrom(src => src.Userroles.Any()
                        ? src.Userroles.First().IdRoleNavigation.IdRole
                        : 0)
                )
                .ForMember(
                    dest => dest.NameRole,
                    opt => opt.MapFrom(src => src.Userroles.Any()
                        ? src.Userroles.First().IdRoleNavigation.NameRole
                        : null)
                )
                .ForMember(
                    dest => dest.StatusRole,
                    opt => opt.MapFrom(src => src.Userroles.Any()
                        ? src.Userroles.First().IdRoleNavigation.StatusRole
                        : (sbyte)0)
                );

            CreateMap<User, UserAdministrationDto>()
                .ForMember(dest => dest.IdRole, opt => opt.MapFrom(src => src.Userroles.Any()
                    ? src.Userroles.First().IdRoleNavigation.IdRole : 0))
                .ForMember(dest => dest.NameRole, opt => opt.MapFrom(src => src.Userroles.Any()
                    ? src.Userroles.First().IdRoleNavigation.NameRole : null))
                .ForMember(dest => dest.StatusRole, opt => opt.MapFrom(src => src.Userroles.Any()
                    ? src.Userroles.First().IdRoleNavigation.StatusRole : (sbyte)0))
                .ForMember(dest => dest.SolutionCenterId, opt => opt.Ignore())
                .ForMember(dest => dest.SolutionCenterCode, opt => opt.Ignore())
                .ForMember(dest => dest.SolutionCenterName, opt => opt.Ignore())
                .AfterMap((src, dest) =>
                {
                    var assignment = src.Userroles.FirstOrDefault();
                    if (assignment is not null)
                    {
                        dest.SolutionCenterId = assignment.SolutionCenterId;
                        dest.SolutionCenterCode = assignment.SolutionCenter?.SolutionCenterCode;
                        dest.SolutionCenterName = assignment.SolutionCenter?.SolutionCenterName;
                    }
                });
        }
    }
}
