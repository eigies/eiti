using eiti.Domain.Customers;

namespace eiti.Application.Abstractions.Repositories;

public interface ICustomerPaymentRepository
{
    Task<CustomerPayment?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    Task<List<CustomerPayment>> ListByCustomerAsync(Guid companyId, Guid customerId, CancellationToken ct = default);

    Task AddAsync(CustomerPayment payment, CancellationToken ct = default);

    // Cobros de cuenta corriente activos del período, para el reporte de medios de pago.
    // Filtra por CreatedAt (misma referencia temporal que Sale.CreatedAt en el resto del reporte)
    // y excluye los anulados. AsNoTracking.
    Task<IReadOnlyList<CustomerPayment>> ListForPaymentMethodsReportAsync(
        Guid companyId,
        DateTime from,
        DateTime to,
        Guid? branchId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken ct = default);
}
