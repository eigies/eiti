using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Commands.UpdateSupplier;

public static class UpdateSupplierErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Suppliers.Update.NotFound",
        "The supplier was not found.");
}
