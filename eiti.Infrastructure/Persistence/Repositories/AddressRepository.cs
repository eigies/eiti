using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Addresses;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class AddressRepository : IAddressRepository
{
    private readonly ApplicationDbContext _context;

    public AddressRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Address?> GetByIdAsync(AddressId id, CancellationToken cancellationToken = default)
    {
        return await _context.Addresses.FirstOrDefaultAsync(address => address.Id == id, cancellationToken);
    }

    public async Task AddAsync(Address address, CancellationToken cancellationToken = default)
    {
        await _context.Addresses.AddAsync(address, cancellationToken);
    }

    public void Update(Address address)
    {
        _context.Addresses.Update(address);
    }
}
