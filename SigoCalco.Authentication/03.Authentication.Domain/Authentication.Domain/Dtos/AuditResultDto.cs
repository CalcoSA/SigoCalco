namespace SigoCalco.Authentication.Domain.Dtos
{
    /// <summary>
    /// Resultado interno de una operación, con la descripción para su auditoría.
    /// No forma parte de la respuesta de los endpoints.
    /// </summary>
    public class AuditResultDto
    {
        public bool IsSuccess { get; init; }
        public UserAdministrationDto? User { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}
