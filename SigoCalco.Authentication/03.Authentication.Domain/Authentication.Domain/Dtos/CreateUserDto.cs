namespace SigoCalco.Authentication.Domain.Dtos
{
    public class CreateUserDto
    {
        public ulong WordpressUserId { get; set; }
        public string? UserLogin { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public bool StatusUser { get; set; }
        public int IdRole { get; set; }
        public int? SolutionCenterId { get; set; }
    }
}
