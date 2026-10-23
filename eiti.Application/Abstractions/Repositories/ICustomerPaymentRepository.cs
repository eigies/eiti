using eiti.Domain.Customers;

namespace eiti.Application.Abstractions.Repositories;

public interface ICustomerPaymentRepository
{
    Task<CustomerPayment?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    Task<List<CustomerPayment>> ListByCustomerAsync(Guid companyId, Guid customerId, CancellationToken ct = default);

    // Medios de un recibo (activos y anulados).
    Task<List<CustomerPayment>> ListByReceiptAsync(Guid receiptId, Guid companyId, CancellationToken ct = default);

    Task AddAsync(CustomerPayment payment, CancellationToken ct = default);

    // Banco receptor de los cobros por transferencia de la lista (solo los que lo tienen), para la caja.
    Task<IReadOnlyDictionary<Guid, int>> GetTransferBanksAsync(
        IReadOnlyCollection<Guid> paymentIds, Guid companyId, CancellationToken ct = default);

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
