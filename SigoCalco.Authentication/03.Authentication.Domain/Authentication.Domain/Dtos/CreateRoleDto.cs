namespace SigoCalco.Authentication.Domain.Dtos
{
    public class CreateRoleDto
    {
        public string? NameRole { get; set; }
        public sbyte StatusRole { get; set; }
        public IEnumerable<int> MenuOptionIds { get; set; } = new List<int>();
    }
}