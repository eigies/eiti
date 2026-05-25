using eiti.Application.Common;

namespace eiti.Application.Features.Purchases.Queries.GetPurchaseById;

public static class GetPurchaseByIdErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Purchases.GetById.NotFound",
        "The purchase was not found.");
}
