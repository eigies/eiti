using eiti.Application.Common;

namespace eiti.Application.Features.Products.Commands.ImportProducts;

public static class ImportProductsErrors
{
    public static readonly Error RowsRequired = Error.Validation(
        "Products.Import.RowsRequired",
        "At least one product row is required.");
}
