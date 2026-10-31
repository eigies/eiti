using eiti.Application.Common;
using eiti.Domain.Banks;
using eiti.Domain.Sales;

namespace eiti.Application.Features.Sales.Common;

/// <summary>
/// Pago con tarjeta de una venta minorista (alta y edición). El monto de la línea de tarjeta que manda el
/// front es la BASE: la parte de la venta que paga esa tarjeta. El recargo lo calcula siempre el back con el
/// plan del banco (nunca se toma un recargo del front) y lo que pasa por el posnet es base + recargo.
/// Una sola tarjeta por venta: SalePayments tiene clave (SaleId, Method) y fusionar dos líneas perdía el
/// banco y las cuotas de la segunda.
/// </summary>
public static class SaleCardPayment
{
    public static Result EnsureSingleCard(IEnumerable<int> paymentMethodIds) =>
        paymentMethodIds.Count(id => id == (int)SalePaymentMethod.Card) > 1
            ? Result.Failure(SalePaymentErrors.SingleCard)
            : Result.Success();

    /// <summary>Plan inexistente, inactivo o al 0 %: sin recargo, se cobra la base.</summary>
    public static SaleCardCharge Charge(Bank? bank, int? cuotas, decimal baseAmount)
    {
        var plan = cuotas is null ? null : CardSurchargeCalculator.FindPlan(bank, cuotas.Value);
        var surcharge = plan is null ? 0m : CardSurchargeCalculator.Compute(baseAmount, plan.SurchargePct);
        return new SaleCardCharge(bank, plan, baseAmount, surcharge);
    }
}

public sealed record SaleCardCharge(Bank? Bank, BankInstallmentPlan? Plan, decimal BaseAmount, decimal Surcharge)
{
    /// <summary>SalePayment.Amount es lo cobrado (base + recargo); el recargo queda además en CardSurchargeAmt.</summary>
    public SalePayment CreatePayment(string? reference)
    {
        var payment = SalePayment.Create(SalePaymentMethod.Card, BaseAmount + Surcharge, reference);
        if (Bank is not null && Plan is not null)
        {
            payment.SetCardData(Bank.Id, Plan.Cuotas, Plan.SurchargePct, Surcharge);
        }

        return payment;
    }
}
