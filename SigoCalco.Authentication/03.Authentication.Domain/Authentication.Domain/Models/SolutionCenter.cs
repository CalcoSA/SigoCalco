namespace SigoCalco.Authentication.Domain.Models
{
    public class SolutionCenter
    {
        public int SolutionCenterId { get; set; }
        public string SolutionCenterCode { get; set; } = null!;
        public string SolutionCenterName { get; set; } = null!;
    }
}
