using System.Net;
using System.Text.Json;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response already started, cannot write error response");
            return;
        }

        var statusCode = exception switch
        {
            AppException appEx => appEx.StatusCode,
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => HttpStatusCode.Conflict,
            Microsoft.EntityFrameworkCore.DbUpdateException => HttpStatusCode.Conflict,
            OperationCanceledException => HttpStatusCode.RequestTimeout,
            FormatException => HttpStatusCode.BadRequest,
            ArgumentException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception: {Message}", exception.Message);
        }

        // In production, only return generic messages for 500 errors
        // For other errors, still return the message (validation errors, etc.)
        var message = _env.IsProduction() && statusCode == HttpStatusCode.InternalServerError
            ? "An internal error occurred"
            : exception.Message;

        var response = new
        {
            success = false,
            message,
            statusCode = (int)statusCode
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
