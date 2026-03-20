using eiti.Api.Extensions;
using eiti.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Tests.Unit;

public sealed class ResultExtensionsTests
{
    [Fact]
    public void ToActionResult_Generic_WhenSuccess_ReturnsOkObjectResult()
    {
        var result = Result.Success("test-value");

        var actionResult = result.ToActionResult();

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be("test-value");
    }

    [Fact]
    public void ToActionResult_NonGeneric_WhenSuccess_ReturnsNoContentResult()
    {
        var result = Result.Success();

        var actionResult = result.ToActionResult();

        actionResult.Should().BeOfType<NoContentResult>();
    }

    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest, "Validation Error")]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound, "Not Found")]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict, "Conflict")]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized, "Unauthorized")]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden, "Forbidden")]
    [InlineData(ErrorType.Failure, StatusCodes.Status500InternalServerError, "Internal Server Error")]
    public void ToActionResult_WhenFailure_ReturnsProblemDetailsWithCorrectStatusAndTitle(
        ErrorType errorType, int expectedStatus, string expectedTitle)
    {
        var error = CreateError(errorType, "TEST_CODE", "Test description");
        var result = Result.Failure<string>(error);

        var actionResult = result.ToActionResult();

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatus);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(expectedStatus);
        problemDetails.Title.Should().Be(expectedTitle);
        problemDetails.Detail.Should().Be("Test description");
        problemDetails.Extensions["errorCode"].Should().Be("TEST_CODE");
    }

    [Fact]
    public void ToActionResult_NonGeneric_WhenFailure_ReturnsProblemDetails()
    {
        var error = Error.Validation("FIELD_INVALID", "Field is invalid");
        var result = Result.Failure(error);

        var actionResult = result.ToActionResult();

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Validation Error");
        problemDetails.Detail.Should().Be("Field is invalid");
    }

    private static Error CreateError(ErrorType type, string code, string description)
    {
        return type switch
        {
            ErrorType.Validation => Error.Validation(code, description),
            ErrorType.NotFound => Error.NotFound(code, description),
            ErrorType.Conflict => Error.Conflict(code, description),
            ErrorType.Unauthorized => Error.Unauthorized(code, description),
            ErrorType.Forbidden => Error.Forbidden(code, description),
            _ => Error.Failure(code, description)
        };
    }
}
