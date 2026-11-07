using System.ComponentModel.DataAnnotations;

namespace SigoCalco.Authentication.Domain.Dtos
{
    public class RefreshTokenDto
    {
        [Required]
        [StringLength(80, MinimumLength = 80)]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
