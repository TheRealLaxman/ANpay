using System.Net;

namespace ANpay.Api.Exceptions;

public class AppException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public AppException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message = "Resource not found")
        : base(message, System.Net.HttpStatusCode.NotFound) { }
}

public class UnauthorizedAccessException : AppException
{
    public UnauthorizedAccessException(string message = "Unauthorized")
        : base(message, System.Net.HttpStatusCode.Unauthorized) { }
}

public class ValidationException : AppException
{
    public ValidationException(string message = "Validation failed")
        : base(message, System.Net.HttpStatusCode.BadRequest) { }
}
