using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.AddCcPaymentGroup;

public static class AddCcPaymentGroupErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized("Sales.AddCcPaymentGroup.Unauthorized", "Authentication is required.");
    public static readonly Error SaleNotFound = Error.NotFound("Sales.AddCcPaymentGroup.SaleNotFound", "Sale not found.");
    public static readonly Error NotCuentaCorriente = Error.Validation("Sales.AddCcPaymentGroup.NotCC", "This operation is only available for Cuenta Corriente sales.");
    public static readonly Error InvalidPaymentMethod = Error.Validation("Sales.AddCcPaymentGroup.InvalidMethod", "One or more payment methods are invalid.");
    public static readonly Error CashSessionRequired = Error.Validation("Sales.AddCcPaymentGroup.CashSessionRequired", "An open cash session is required for the selected cash drawer.");
}
