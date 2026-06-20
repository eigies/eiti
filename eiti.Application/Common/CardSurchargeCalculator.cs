using eiti.Domain.Banks;

namespace eiti.Application.Common;

/// <summary>
/// Cálculo del recargo de tarjeta por plan de cuotas. Unifica la búsqueda del plan activo
/// y el redondeo del recargo que estaban duplicados en CreateSale y AddCustomerPayment.
/// La base sobre la que se aplica el recargo (subtotal de la venta vs. monto del cobro) la decide el handler.
/// </summary>
public static class CardSurchargeCalculator
{
    /// <summary>Plan de cuotas activo del banco que coincide con la cantidad de cuotas, o null.</summary>
    public static BankInstallmentPlan? FindPlan(Bank? bank, int cuotas) =>
        bank?.InstallmentPlans.FirstOrDefault(p => p.Cuotas == cuotas && p.Active);

    /// <summary>Recargo = baseAmount * surchargePct / 100, redondeado a 2 decimales (AwayFromZero).</summary>
    public static decimal Compute(decimal baseAmount, decimal surchargePct) =>
        decimal.Round(baseAmount * surchargePct / 100m, 2, MidpointRounding.AwayFromZero);
}
