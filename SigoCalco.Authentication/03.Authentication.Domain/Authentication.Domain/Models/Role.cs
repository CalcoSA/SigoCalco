namespace SigoCalco.Authentication.Domain.Models
{
    public partial class Role
    {
        public const string PointOfSaleCode = "PDV";

        public int IdRole { get; set; }
        public string NameRole { get; set; } = null!;
        public string? RoleCode { get; set; }
        public sbyte StatusRole { get; set; }
        public virtual ICollection<Rolemenuoption> Rolemenuoptions { get; set; } = new List<Rolemenuoption>();
        public virtual ICollection<Userrole> Userroles { get; set; } = new List<Userrole>();
    }
}
