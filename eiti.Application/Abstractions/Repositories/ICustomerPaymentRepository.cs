using eiti.Domain.Customers;

namespace eiti.Application.Abstractions.Repositories;

public interface ICustomerPaymentRepository
{
    Task<CustomerPayment?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    Task<List<CustomerPayment>> ListByCustomerAsync(Guid companyId, Guid customerId, CancellationToken ct = default);

    Task AddAsync(CustomerPayment payment, CancellationToken ct = default);
}
