namespace SigoCalco.Authentication.Domain.Dtos
{
    public class IntranetAccessDto
    {
        public string? UserLogin { get; set; }
        public long Ts { get; set; }
        public string? Sig { get; set; }
    }
}