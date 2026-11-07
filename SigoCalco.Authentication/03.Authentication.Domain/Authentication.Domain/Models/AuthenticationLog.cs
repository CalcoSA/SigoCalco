namespace SigoCalco.Authentication.Domain.Models
{
    public partial class AuthenticationLog
    {
        public long LogId { get; set; }

        public string Action { get; set; } = null!;

        public string Module { get; set; } = null!;

        public string Description { get; set; } = null!;

        public string UserName { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
