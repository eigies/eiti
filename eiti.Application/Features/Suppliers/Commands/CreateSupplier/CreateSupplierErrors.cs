using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Commands.CreateSupplier;

public static class CreateSupplierErrors
{
    public static readonly Error NameAlreadyExists = Error.Conflict(
        "Suppliers.Create.NameAlreadyExists",
        "A supplier with this name already exists for this company.");
}
