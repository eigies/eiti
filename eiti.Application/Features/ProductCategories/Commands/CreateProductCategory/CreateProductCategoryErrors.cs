using eiti.Application.Common;

namespace eiti.Application.Features.ProductCategories.Commands.CreateProductCategory;

public static class CreateProductCategoryErrors
{
    public static readonly Error NameAlreadyExists = Error.Conflict(
        "ProductCategories.Create.NameAlreadyExists",
        "Ya existe una categoría con ese nombre.");
}
