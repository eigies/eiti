using System.Text.Json;
using eiti.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace eiti.Tests;

public sealed class GlobalExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ShouldReturn500WithGenericMessage_WhenUnhandledExceptionIsThrown()
    {
        var logger = new Mock<ILogger<GlobalExceptionHandlingMiddleware>>();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Super secret internal error details"),
            logger.Object);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        body.Should().Contain("An unexpected error occurred. Please try again later.");
        body.Should().NotContain("Super secret internal error details");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("Detail").GetString()
            .Should().Be("An unexpected error occurred. Please try again later.");
    }
}
