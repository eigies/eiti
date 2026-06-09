using eiti.Application.Common;

namespace eiti.Application.Features.ProductCategories.Commands.DeleteProductCategory;

public static class DeleteProductCategoryErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ProductCategories.Delete.NotFound",
        "La categoría no fue encontrada.");
}
