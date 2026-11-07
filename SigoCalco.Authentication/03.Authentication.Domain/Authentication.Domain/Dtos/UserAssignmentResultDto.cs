namespace SigoCalco.Authentication.Domain.Dtos
{
    // Internal result: audit values read in the same transaction as the assignment.
    public class UserAssignmentResultDto
    {
        public string? PreviousSolutionCenterCode { get; init; }
        public string? SolutionCenterCode { get; init; }
    }
}
