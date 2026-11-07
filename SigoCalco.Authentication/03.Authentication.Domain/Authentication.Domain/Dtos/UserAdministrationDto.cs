using System.Text.Json.Serialization;

namespace SigoCalco.Authentication.Domain.Dtos
{
    public class UserAdministrationDto : UserDto
    {
        // Nullable only for diagnosing historical users without an assignment.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SolutionCenterId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SolutionCenterCode { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SolutionCenterName { get; set; }
    }
}
