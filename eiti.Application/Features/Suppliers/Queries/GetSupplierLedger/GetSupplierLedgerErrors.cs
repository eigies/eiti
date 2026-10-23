using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Queries.GetSupplierLedger;

public static class GetSupplierLedgerErrors
{
    public static readonly Error SupplierNotFound = Error.NotFound(
        "Suppliers.GetLedger.SupplierNotFound",
        "El proveedor no existe.");
}
