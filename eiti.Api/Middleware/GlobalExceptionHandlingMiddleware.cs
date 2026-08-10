using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace eiti.Api.Middleware;

public sealed class GlobalExceptionHandlingMiddleware
{
    // ProblemDetails de MVC (via ResultExtensions) sale en camelCase. Sin estas opciones el
    // serializador respeta los nombres declarados (PascalCase) y el front, que lee `detail`,
    // nunca encuentra el mensaje: todo 500 termina mostrando el texto generico del cliente.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, response) = exception switch
        {
            ValidationException validationException => (
                (int)HttpStatusCode.BadRequest,
                (object)new
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Validation Error",
                    Errors = validationException.Errors.Select(e => new
                    {
                        e.PropertyName,
                        e.ErrorMessage
                    })
                }),
            DbUpdateException dbUpdateException when IsUniqueConstraintViolation(dbUpdateException) => (
                (int)HttpStatusCode.Conflict,
                (object)new
                {
                    Status = (int)HttpStatusCode.Conflict,
                    Title = "Conflict",
                    Detail = "La operación ya fue procesada o hubo un envío duplicado. Refrescá y verificá antes de reintentar.",
                    ErrorCode = "Common.DuplicateSubmission"
                }),
            _ => (
                (int)HttpStatusCode.InternalServerError,
                (object)new
                {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred. Please try again later."
                })
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }

    // PostgreSQL: SQLSTATE 23505 = unique_violation (índice/constraint único duplicado).
    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException &&
        postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
}
