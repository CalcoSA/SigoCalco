namespace SigoCalco.Authentication.Domain.Models
{
    public partial class Menuoption
    {
        public int IdMenuOption { get; set; }
        public string NameMenuOption { get; set; } = null!;
        public string? PathMenuOption { get; set; } = null!;
        public int? ParentMenuOption { get; set; }
        public int OrderMenuOption { get; set; }
        public sbyte StatusMenuOption { get; set; }
        public virtual ICollection<Menuoption> InverseParentMenuOptionNavigation { get; set; } = new List<Menuoption>();
        public virtual Menuoption ParentMenuOptionNavigation { get; set; } = null!;
        public virtual ICollection<Rolemenuoption> Rolemenuoptions { get; set; } = new List<Rolemenuoption>();
    }
}