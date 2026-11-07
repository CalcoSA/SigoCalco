namespace SigoCalco.Authentication.Domain.Dtos
{
    public class UpdateRoleDto
    {
        public int IdRole { get; set; }
        public string? NameRole { get; set; }
        public sbyte StatusRole { get; set; }
        public IEnumerable<int> MenuOptionIds { get; set; } = new List<int>();
    }
}