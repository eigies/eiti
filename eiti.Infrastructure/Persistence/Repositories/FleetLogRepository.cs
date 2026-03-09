using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Fleet;
using eiti.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class FleetLogRepository : IFleetLogRepository
{
    private readonly ApplicationDbContext _context;

    public FleetLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(FleetLog log, CancellationToken cancellationToken = default)
    {
        await _context.FleetLogs.AddAsync(log, cancellationToken);
    }

    public async Task<IReadOnlyList<FleetLog>> ListByVehicleAsync(
        VehicleId vehicleId,
        CompanyId companyId,
        DateTime? from = null,
        DateTime? to = null,
        FleetLogType? type = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.FleetLogs.Where(x => x.VehicleId == vehicleId && x.CompanyId == companyId);

        if (from.HasValue)
        {
            query = query.Where(x => x.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.OccurredAt <= to.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(x => x.Type == type.Value);
        }

        return await query.OrderByDescending(x => x.OccurredAt).ToListAsync(cancellationToken);
    }
}
