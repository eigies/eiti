using eiti.Domain.Companies;
using eiti.Domain.Fleet;
using eiti.Domain.Vehicles;

namespace eiti.Application.Abstractions.Repositories;

public interface IFleetLogRepository
{
    Task AddAsync(FleetLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FleetLog>> ListByVehicleAsync(
        VehicleId vehicleId,
        CompanyId companyId,
        DateTime? from = null,
        DateTime? to = null,
        FleetLogType? type = null,
        CancellationToken cancellationToken = default);
}
