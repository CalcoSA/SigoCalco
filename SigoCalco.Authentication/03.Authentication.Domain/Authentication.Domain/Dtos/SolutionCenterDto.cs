namespace SigoCalco.Authentication.Domain.Dtos
{
    public class SolutionCenterDto
    {
        public int SolutionCenterId { get; set; }
        public string SolutionCenterCode { get; set; } = null!;
        public string SolutionCenterName { get; set; } = null!;
    }
}
