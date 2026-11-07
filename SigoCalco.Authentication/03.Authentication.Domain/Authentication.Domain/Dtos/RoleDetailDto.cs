namespace SigoCalco.Authentication.Domain.Dtos
{
    public class RoleDetailDto
    {
        public int IdRole { get; set; }
        public string? NameRole { get; set; }
        public string? RoleCode { get; set; }
        public sbyte StatusRole { get; set; }
        public IEnumerable<MenuOptionDto> MenuOptions { get; set; } = new List<MenuOptionDto>();
    }
}
