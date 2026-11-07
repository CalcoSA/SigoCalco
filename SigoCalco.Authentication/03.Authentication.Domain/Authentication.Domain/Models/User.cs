namespace SigoCalco.Authentication.Domain.Models
{
    public partial class User
    {
        public int IdUser { get; set; }
        public ulong WordpressUserId { get; set; }
        public string UserLogin { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public bool StatusUser { get; set; }
        public virtual ICollection<Userrole> Userroles { get; set; } = new List<Userrole>();
    }
}