using eiti.Domain.Addresses;

namespace eiti.Application.Abstractions.Repositories;

public interface IAddressRepository
{
    Task<Address?> GetByIdAsync(
        AddressId id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Address address,
        CancellationToken cancellationToken = default);

    void Update(Address address);
    
}
