using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Commands.DeactivateSupplier;

public static class DeactivateSupplierErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Suppliers.Deactivate.NotFound",
        "The supplier was not found.");
}
