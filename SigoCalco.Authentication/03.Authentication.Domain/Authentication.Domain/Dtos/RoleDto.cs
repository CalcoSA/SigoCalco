namespace SigoCalco.Authentication.Domain.Dtos
{
    public class RoleDto
    {
        public int IdRole { get; set; }
        public string? NameRole { get; set; }
        public string? RoleCode { get; set; }
        public sbyte StatusRole { get; set; }
    }
}
