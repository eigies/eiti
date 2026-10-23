using eiti.Domain.Customers;

namespace eiti.Application.Abstractions.Repositories;

public interface ICustomerReceiptRepository
{
    Task<CustomerReceipt?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    // Para numerar RC-#### por empresa. Cuenta todos, incluidos los anulados: el número emitido
    // no se reutiliza.
    Task<int> CountByCompanyAsync(Guid companyId, CancellationToken ct = default);

    Task<List<CustomerReceipt>> ListByCustomerAsync(Guid companyId, Guid customerId, CancellationToken ct = default);

    Task AddAsync(CustomerReceipt receipt, CancellationToken ct = default);
}
