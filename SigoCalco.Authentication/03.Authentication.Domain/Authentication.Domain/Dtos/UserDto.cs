namespace SigoCalco.Authentication.Domain.Dtos
{
    public class UserDto
    {
        public int IdUser { get; set; }
        public ulong WordpressUserId { get; set; }
        public string? UserLogin { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public bool StatusUser { get; set; }
        public int IdRole { get; set; }
        public string? NameRole { get; set; }
        public sbyte StatusRole { get; set; }
    }
}