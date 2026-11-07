namespace SigoCalco.Authentication.Domain.Exceptions
{
    public class SessionException : Exception
    {
        public string Code { get; }
        public DateTime? ExpiresAt { get; }
        public int? RetryAfterSeconds { get; }

        public SessionException(string code, string message, DateTime? expiresAt = null,
            int? retryAfterSeconds = null) : base(message)
        {
            Code = code;
            ExpiresAt = expiresAt;
            RetryAfterSeconds = retryAfterSeconds;
        }
    }
}
