using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly ApplicationDbContext _context;

    public CustomerRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> GetByIdAsync(CustomerId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers.FirstOrDefaultAsync(customer => customer.Id == id && customer.CompanyId == companyId, cancellationToken);
    }

    public async Task<Customer?> GetByEmailAsync(Email email, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers.FirstOrDefaultAsync(customer => customer.Email == email && customer.CompanyId == companyId, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(Email email, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers.AnyAsync(customer => customer.Email == email && customer.CompanyId == companyId, cancellationToken);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _context.Customers.AddAsync(customer, cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> SearchAsync(
        CompanyId companyId,
        string? query,
        string? email = null,
        string? documentNumber = null,
        CancellationToken cancellationToken = default)
    {
        var customers = await _context.Customers
            .Where(customer => customer.CompanyId == companyId)
            .OrderBy(customer => customer.Name)
            .ToListAsync(cancellationToken);

        IEnumerable<Customer> filtered = customers;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var trimmed = query.Trim();
            var normalized = trimmed.ToLowerInvariant();
            filtered = filtered.Where(customer =>
                customer.Name.ToLowerInvariant().Contains(normalized) ||
                (customer.Email != null && customer.Email.Value.ToLowerInvariant().Contains(normalized)) ||
                (!string.IsNullOrWhiteSpace(customer.DocumentNumber) && customer.DocumentNumber.Contains(trimmed, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.Trim().ToLowerInvariant();
            filtered = filtered.Where(customer => customer.Email != null && customer.Email.Value.ToLowerInvariant() == normalized);
        }

        if (!string.IsNullOrWhiteSpace(documentNumber))
        {
            var normalized = documentNumber.Trim();
            filtered = filtered.Where(customer => string.Equals(customer.DocumentNumber, normalized, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToList();
    }

    public async Task<bool> DocumentExistsAsync(
        CompanyId companyId,
        DocumentType documentType,
        string documentNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.Customers.AnyAsync(
            customer => customer.CompanyId == companyId && customer.DocumentType == documentType && customer.DocumentNumber == documentNumber,
            cancellationToken);
    }

    public async Task<bool> TaxIdExistsAsync(CompanyId companyId, string taxId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers.AnyAsync(customer => customer.CompanyId == companyId && customer.TaxId == taxId, cancellationToken);
    }

    public async Task<bool> IsReferencedAsync(CustomerId customerId, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.Sales.AnyAsync(
            sale => sale.CompanyId == companyId && sale.CustomerId == customerId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> ListByIdsAsync(
        CompanyId companyId,
        IEnumerable<CustomerId> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        return await _context.Customers
            .Where(customer => customer.CompanyId == companyId && idList.Contains(customer.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> ListWithPositiveCreditAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .Where(customer => customer.CompanyId == companyId && customer.CreditBalance > 0)
            .ToListAsync(cancellationToken);
    }

    public void Update(Customer customer)
    {
        _context.Customers.Update(customer);
    }

    public void Delete(Customer customer)
    {
        _context.Customers.Remove(customer);
    }
}
