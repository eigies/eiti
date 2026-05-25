namespace eiti.Application.Features.Suppliers.Queries.ListSuppliers;

public sealed record ListSuppliersResponse(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? TaxId,
    bool IsActive);
