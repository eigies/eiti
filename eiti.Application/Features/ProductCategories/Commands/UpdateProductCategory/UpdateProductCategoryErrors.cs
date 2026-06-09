using eiti.Application.Common;

namespace eiti.Application.Features.ProductCategories.Commands.UpdateProductCategory;

public static class UpdateProductCategoryErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ProductCategories.Update.NotFound",
        "La categoría no fue encontrada.");

    public static readonly Error NameAlreadyExists = Error.Conflict(
        "ProductCategories.Update.NameAlreadyExists",
        "Ya existe otra categoría con ese nombre.");
}
