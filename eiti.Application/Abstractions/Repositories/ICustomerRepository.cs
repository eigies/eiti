using eiti.Domain.Customers;
using eiti.Domain.Companies;

namespace eiti.Application.Abstractions.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(
        CustomerId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<Customer?> GetByEmailAsync(
        Email email,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(
        Email email,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Customer customer, 
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Customer>> SearchAsync(
        CompanyId companyId,
        string? query,
        string? email = null,
        string? documentNumber = null,
        CancellationToken cancellationToken = default);

    Task<bool> DocumentExistsAsync(
        CompanyId companyId,
        DocumentType documentType,
        string documentNumber,
        CancellationToken cancellationToken = default);

    Task<bool> TaxIdExistsAsync(
        CompanyId companyId,
        string taxId,
        CancellationToken cancellationToken = default);

    void Update(Customer customer);

    void Delete(Customer customer);
}
