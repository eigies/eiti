using eiti.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        return result.IsSuccess
            ? new OkObjectResult(result.Value)
            : ToProblemDetails(result);
    }

    public static IActionResult ToActionResult(this Result result)
    {
        return result.IsSuccess
            ? new NoContentResult()
            : ToProblemDetails(result);
    }

    private static IActionResult ToProblemDetails(Result result)
    {
        var (statusCode, title) = GetErrorMapping(result.Error.Type);

        return new ObjectResult(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = result.Error.Description,
            Extensions = { ["errorCode"] = result.Error.Code }
        })
        {
            StatusCode = statusCode
        };
    }

    private static (int StatusCode, string Title) GetErrorMapping(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Validation Error"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };
    }
}
