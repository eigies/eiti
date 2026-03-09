using eiti.Application.Common;

namespace eiti.Application.Features.Products.Commands.CreateProduct;

public static class CreateProductErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Products.Create.Unauthorized",
        "The current user is not authenticated.");

    public static readonly Error ProductNameAlreadyExists = Error.Conflict(
        "Products.Create.NameAlreadyExists",
        "A product with that name already exists for the current company.");
}
