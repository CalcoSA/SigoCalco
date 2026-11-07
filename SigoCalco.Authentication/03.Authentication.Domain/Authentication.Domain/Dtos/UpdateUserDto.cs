namespace SigoCalco.Authentication.Domain.Dtos
{
    public class UpdateUserDto
    {
        public int IdUser { get; set; }
        public bool? StatusUser { get; set; }
        public int? IdRole { get; set; }
        public int? SolutionCenterId { get; set; }
        public string? Email { get; set; }
    }
}
