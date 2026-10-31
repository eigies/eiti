using eiti.Application.Abstractions.Repositories;
using eiti.Application.Common;
using eiti.Domain.Companies;
using eiti.Domain.Sales;

namespace eiti.Application.Features.Sales.Common;

public sealed record RetailDiscountLine(Guid ProductId, decimal DiscountPercent);

/// <summary>
/// Tope de descuento de la venta minorista (Company.MaxRetailDiscountPercent). Lo usan el alta y la edición.
/// </summary>
public interface IRetailDiscountPolicy
{
    /// <param name="current">La venta tal como está guardada (null en el alta). Solo se valida lo que cambia
    /// respecto de ella: bajar el tope no bloquea editar una venta en espera que ya tenía el descuento.</param>
    Task<Result> EnsureWithinLimitAsync(
        CompanyId companyId,
        decimal generalDiscountPercent,
        IReadOnlyCollection<RetailDiscountLine> lines,
        Sale? current,
        CancellationToken cancellationToken);
}

public sealed class RetailDiscountPolicy : IRetailDiscountPolicy
{
    private readonly ICompanyRepository _companyRepository;

    public RetailDiscountPolicy(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<Result> EnsureWithinLimitAsync(
        CompanyId companyId,
        decimal generalDiscountPercent,
        IReadOnlyCollection<RetailDiscountLine> lines,
        Sale? current,
        CancellationToken cancellationToken)
    {
        // Las ventas de cuenta corriente no tienen tope.
        if (current?.IsCuentaCorriente == true)
        {
            return Result.Success();
        }

        var touched = TouchedDiscounts(generalDiscountPercent, lines, current);
        if (touched.Count == 0)
        {
            return Result.Success();
        }

        // Sin empresa no hay tope fijado: rige el default (0 = descuento deshabilitado).
        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        var limit = company?.MaxRetailDiscountPercent ?? 0m;

        return touched.Max() > limit
            ? Result.Failure(RetailDiscountErrors.ExceedsLimit(limit))
            : Result.Success();
    }

    // Los descuentos (> 0) que se fijan o cambian respecto de la venta guardada.
    private static List<decimal> TouchedDiscounts(
        decimal generalDiscountPercent,
        IReadOnlyCollection<RetailDiscountLine> lines,
        Sale? current)
    {
        var touched = new List<decimal>();

        var general = Round(generalDiscountPercent);
        if (general > 0m && general != Round(current?.GeneralDiscountPercent ?? 0m))
        {
            touched.Add(general);
        }

        var previousByProduct = current?.Details
            .GroupBy(detail => detail.ProductId.Value)
            .ToDictionary(group => group.Key, group => Round(group.First().DiscountPercent))
            ?? [];

        foreach (var line in lines)
        {
            var discount = Round(line.DiscountPercent);
            if (discount <= 0m)
            {
                continue;
            }

            if (previousByProduct.TryGetValue(line.ProductId, out var previous) && previous == discount)
            {
                continue;
            }

            touched.Add(discount);
        }

        return touched;
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
