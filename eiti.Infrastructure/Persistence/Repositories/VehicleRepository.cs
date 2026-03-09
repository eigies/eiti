using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly ApplicationDbContext _context;

    public VehicleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        await _context.Vehicles.AddAsync(vehicle, cancellationToken);
    }

    public Task<Vehicle?> GetByIdAsync(VehicleId id, CompanyId companyId, CancellationToken cancellationToken = default) =>
        _context.Vehicles.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default) =>
        await _context.Vehicles.Where(x => x.CompanyId == companyId).OrderBy(x => x.Plate).ToListAsync(cancellationToken);

    public async Task<bool> PlateExistsAsync(CompanyId companyId, string plate, VehicleId? excludingId, CancellationToken cancellationToken = default) =>
        await _context.Vehicles.AnyAsync(
            x => x.CompanyId == companyId
                && x.Plate == plate
                && (excludingId == null || x.Id != excludingId),
            cancellationToken);
}
