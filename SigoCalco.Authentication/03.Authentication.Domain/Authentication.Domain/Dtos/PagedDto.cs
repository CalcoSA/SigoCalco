namespace SigoCalco.Authentication.Domain.Dtos
{
    public class PagedDto<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int Take { get; set; }
        public int Pages { get; set; }
    }
}