namespace SigoCalco.Authentication.Domain.Responses
{
    public class ResponseApi
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = null!;
        public object Result { get; set; } = null!;
    }
}