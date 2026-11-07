namespace SigoCalco.Authentication.Domain.Models
{
    public partial class Rolemenuoption
    {
        public int IdRoleMenuOption { get; set; }
        public int IdRole { get; set; }
        public int IdMenuOption { get; set; }
        public virtual Menuoption IdMenuOptionNavigation { get; set; } = null!;
        public virtual Role IdRoleNavigation { get; set; } = null!;
    }
}