namespace SigoCalco.Authentication.Domain.Dtos
{
    public class MenuOptionDto
    {
        public int IdMenuOption { get; set; }
        public string? NameMenuOption { get; set; }
        public string? PathMenuOption { get; set; }
        public int? ParentMenuOption { get; set; }
        public int OrderMenuOption { get; set; }
        public sbyte StatusMenuOption { get; set; }
    }
}