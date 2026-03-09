using eiti.Domain.Companies;
using eiti.Domain.Vehicles;

namespace eiti.Application.Abstractions.Repositories;

public interface IVehicleRepository
{
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByIdAsync(VehicleId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vehicle>> ListByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default);
    Task<bool> PlateExistsAsync(CompanyId companyId, string plate, VehicleId? excludingId, CancellationToken cancellationToken = default);
}
