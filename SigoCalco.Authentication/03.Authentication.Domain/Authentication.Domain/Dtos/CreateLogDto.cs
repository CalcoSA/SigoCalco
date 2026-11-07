namespace SigoCalco.Authentication.Domain.Dtos
{
    public class CreateLogDto
    {
        public string Action { get; set; } = null!;

        public string Module { get; set; } = null!;

        public string Description { get; set; } = null!;

        public string UserName { get; set; } = null!;
    }
}
