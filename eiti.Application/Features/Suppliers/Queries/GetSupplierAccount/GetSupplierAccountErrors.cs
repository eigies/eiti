using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Queries.GetSupplierAccount;

public static class GetSupplierAccountErrors
{
    public static readonly Error SupplierNotFound = Error.NotFound(
        "Suppliers.Account.NotFound",
        "El proveedor no existe.");
}
