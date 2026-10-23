using eiti.Domain.Purchases;
using eiti.Domain.Sales;

namespace eiti.Application.Common.Accounts;

// Código y etiqueta de un medio de pago para el extracto, unificando los enums de proveedores y clientes.
public static class PaymentMeansLabels
{
    public static (string Method, string Label) For(PurchasePaymentMethod method) => method switch
    {
        PurchasePaymentMethod.Cash => ("cash", "Efectivo"),
        PurchasePaymentMethod.BankTransfer => ("transfer", "Transferencia"),
        PurchasePaymentMethod.Check => ("check", "Cheque"),
        _ => ("other", "Otro"),
    };

    public static (string Method, string Label) For(SalePaymentMethod method) => method switch
    {
        SalePaymentMethod.Cash => ("cash", "Efectivo"),
        SalePaymentMethod.Transfer => ("transfer", "Transferencia"),
        SalePaymentMethod.Card => ("card", "Tarjeta"),
        SalePaymentMethod.Check => ("check", "Cheque"),
        _ => ("other", "Otro"),
    };
}
