using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Branches;
using eiti.Domain.Employees;
using eiti.Domain.Fleet;
using eiti.Domain.Vehicles;
using MediatR;

namespace eiti.Application.Features.Vehicles;

public sealed record VehicleResponse(
    Guid Id,
    Guid? BranchId,
    Guid? AssignedDriverEmployeeId,
    string? AssignedDriverFullName,
    string Plate,
    string Model,
    string? Brand,
    int? Year,
    int FuelType,
    string FuelTypeName,
    decimal? CurrentOdometer,
    DateTime? LastFuelLoadedAt,
    DateTime? LastMaintenanceAt,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record FleetLogResponse(
    Guid Id,
    Guid VehicleId,
    Guid? PerformedByEmployeeId,
    string? PerformedByEmployeeName,
    int Type,
    string TypeName,
    DateTime OccurredAt,
    decimal? Odometer,
    decimal? FuelLiters,
    decimal? FuelCost,
    string? MaintenanceType,
    string Description,
    string? Notes,
    DateTime CreatedAt);

public sealed record CreateVehicleCommand(
    Guid? BranchId,
    Guid? AssignedDriverEmployeeId,
    string Plate,
    string Model,
    string? Brand,
    int? Year,
    int FuelType,
    decimal? CurrentOdometer,
    string? Notes) : IRequest<Result<VehicleResponse>>;

public sealed record UpdateVehicleCommand(
    Guid Id,
    Guid? BranchId,
    Guid? AssignedDriverEmployeeId,
    string Plate,
    string Model,
    string? Brand,
    int? Year,
    int FuelType,
    decimal? CurrentOdometer,
    string? Notes) : IRequest<Result<VehicleResponse>>;

public sealed record AssignVehicleDriverCommand(Guid Id, Guid EmployeeId) : IRequest<Result<VehicleResponse>>;
public sealed record UnassignVehicleDriverCommand(Guid Id) : IRequest<Result<VehicleResponse>>;
public sealed record DeactivateVehicleCommand(Guid Id) : IRequest<Result<VehicleResponse>>;
public sealed record GetVehicleQuery(Guid Id) : IRequest<Result<VehicleResponse>>;
public sealed record ListVehiclesQuery() : IRequest<Result<IReadOnlyList<VehicleResponse>>>;

public sealed record CreateFleetLogCommand(
    Guid VehicleId,
    Guid? PerformedByEmployeeId,
    int Type,
    DateTime OccurredAt,
    decimal? Odometer,
    decimal? FuelLiters,
    decimal? FuelCost,
    string? MaintenanceType,
    string Description,
    string? Notes) : IRequest<Result<FleetLogResponse>>;

public sealed record ListFleetLogsQuery(Guid VehicleId, DateTime? From, DateTime? To, int? Type) : IRequest<Result<IReadOnlyList<FleetLogResponse>>>;

public sealed class CreateVehicleHandler : IRequestHandler<CreateVehicleCommand, Result<VehicleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDriverProfileRepository _driverProfileRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateVehicleHandler(ICurrentUserService currentUserService, IVehicleRepository vehicleRepository, IEmployeeRepository employeeRepository, IDriverProfileRepository driverProfileRepository, IBranchRepository branchRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _vehicleRepository = vehicleRepository;
        _employeeRepository = employeeRepository;
        _driverProfileRepository = driverProfileRepository;
        _branchRepository = branchRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VehicleResponse>> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<VehicleResponse>.Failure(Error.Unauthorized("Vehicles.Create.Unauthorized", "The current user is not authenticated."));
        }

        var branchId = await VehicleRules.ResolveBranchAsync(request.BranchId, _currentUserService.CompanyId, _branchRepository, cancellationToken);
        if (request.BranchId.HasValue && branchId is null)
        {
            return Result<VehicleResponse>.Failure(Error.NotFound("Vehicles.Create.BranchNotFound", "The requested branch was not found."));
        }

        if (!Enum.IsDefined(typeof(FuelType), request.FuelType))
        {
            return Result<VehicleResponse>.Failure(Error.Validation("Vehicles.Create.InvalidFuelType", "The selected fuel type is invalid."));
        }

        var normalizedPlate = request.Plate.Trim().ToUpperInvariant();
        if (await _vehicleRepository.PlateExistsAsync(_currentUserService.CompanyId, normalizedPlate, null, cancellationToken))
        {
            return Result<VehicleResponse>.Failure(Error.Conflict("Vehicles.Create.PlateExists", "A vehicle with the same plate already exists."));
        }

        var driver = await VehicleRules.ValidateDriverAsync(request.AssignedDriverEmployeeId, _currentUserService.CompanyId, _employeeRepository, _driverProfileRepository, cancellationToken);
        if (request.AssignedDriverEmployeeId.HasValue && driver is null)
        {
            return Result<VehicleResponse>.Failure(Error.Validation("Vehicles.Create.InvalidDriver", "The selected driver is invalid or missing a driver profile."));
        }

        Vehicle vehicle;
        try
        {
            vehicle = Vehicle.Create(_currentUserService.CompanyId, branchId, driver?.Id, request.Plate, request.Model, request.Brand, request.Year, (FuelType)request.FuelType, request.CurrentOdometer, request.Notes);
        }
        catch (ArgumentException ex)
        {
            return Result<VehicleResponse>.Failure(Error.Validation("Vehicles.Create.InvalidInput", ex.Message));
        }

        await _vehicleRepository.AddAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<VehicleResponse>.Success(VehicleMappings.Map(vehicle, driver));
    }
}

public sealed class UpdateVehicleHandler : IRequestHandler<UpdateVehicleCommand, Result<VehicleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDriverProfileRepository _driverProfileRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateVehicleHandler(ICurrentUserService currentUserService, IVehicleRepository vehicleRepository, IEmployeeRepository employeeRepository, IDriverProfileRepository driverProfileRepository, IBranchRepository branchRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _vehicleRepository = vehicleRepository;
        _employeeRepository = employeeRepository;
        _driverProfileRepository = driverProfileRepository;
        _branchRepository = branchRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VehicleResponse>> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<VehicleResponse>.Failure(Error.Unauthorized("Vehicles.Update.Unauthorized", "The current user is not authenticated."));
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(new VehicleId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (vehicle is null)
        {
            return Result<VehicleResponse>.Failure(Error.NotFound("Vehicles.Update.NotFound", "The requested vehicle was not found."));
        }

        if (!Enum.IsDefined(typeof(FuelType), request.FuelType))
        {
            return Result<VehicleResponse>.Failure(Error.Validation("Vehicles.Update.InvalidFuelType", "The selected fuel type is invalid."));
        }

        var branchId = await VehicleRules.ResolveBranchAsync(request.BranchId, _currentUserService.CompanyId, _branchRepository, cancellationToken);
        if (request.BranchId.HasValue && branchId is null)
        {
            return Result<VehicleResponse>.Failure(Error.NotFound("Vehicles.Update.BranchNotFound", "The requested branch was not found."));
        }

        var normalizedPlate = request.Plate.Trim().ToUpperInvariant();
        if (await _vehicleRepository.PlateExistsAsync(_currentUserService.CompanyId, normalizedPlate, vehicle.Id, cancellationToken))
        {
            return Result<VehicleResponse>.Failure(Error.Conflict("Vehicles.Update.PlateExists", "A vehicle with the same plate already exists."));
        }

        var driver = await VehicleRules.ValidateDriverAsync(request.AssignedDriverEmployeeId, _currentUserService.CompanyId, _employeeRepository, _driverProfileRepository, cancellationToken);
        if (request.AssignedDriverEmployeeId.HasValue && driver is null)
        {
            return Result<VehicleResponse>.Failure(Error.Validation("Vehicles.Update.InvalidDriver", "The selected driver is invalid or missing a driver profile."));
        }

        try
        {
            vehicle.Update(branchId, request.Plate, request.Model, request.Brand, request.Year, (FuelType)request.FuelType, request.CurrentOdometer, request.Notes);
            if (driver is null)
            {
                vehicle.UnassignDriver();
            }
            else
            {
                vehicle.AssignDriver(driver.Id);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<VehicleResponse>.Failure(Error.Validation("Vehicles.Update.InvalidInput", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<VehicleResponse>.Success(VehicleMappings.Map(vehicle, driver));
    }
}

public sealed class AssignVehicleDriverHandler : IRequestHandler<AssignVehicleDriverCommand, Result<VehicleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDriverProfileRepository _driverProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignVehicleDriverHandler(ICurrentUserService currentUserService, IVehicleRepository vehicleRepository, IEmployeeRepository employeeRepository, IDriverProfileRepository driverProfileRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _vehicleRepository = vehicleRepository;
        _employeeRepository = employeeRepository;
        _driverProfileRepository = driverProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VehicleResponse>> Handle(AssignVehicleDriverCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<VehicleResponse>.Failure(Error.Unauthorized("Vehicles.AssignDriver.Unauthorized", "The current user is not authenticated."));
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(new VehicleId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (vehicle is null)
        {
            return Result<VehicleResponse>.Failure(Error.NotFound("Vehicles.AssignDriver.NotFound", "The requested vehicle was not found."));
        }

        var driver = await VehicleRules.ValidateDriverAsync(request.EmployeeId, _currentUserService.CompanyId, _employeeRepository, _driverProfileRepository, cancellationToken);
        if (driver is null)
        {
            return Result<VehicleResponse>.Failure(Error.Validation("Vehicles.AssignDriver.InvalidDriver", "The selected driver is invalid or missing a driver profile."));
        }

        vehicle.AssignDriver(driver.Id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<VehicleResponse>.Success(VehicleMappings.Map(vehicle, driver));
    }
}

public sealed class UnassignVehicleDriverHandler : IRequestHandler<UnassignVehicleDriverCommand, Result<VehicleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UnassignVehicleDriverHandler(ICurrentUserService currentUserService, IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VehicleResponse>> Handle(UnassignVehicleDriverCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<VehicleResponse>.Failure(Error.Unauthorized("Vehicles.UnassignDriver.Unauthorized", "The current user is not authenticated."));
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(new VehicleId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (vehicle is null)
        {
            return Result<VehicleResponse>.Failure(Error.NotFound("Vehicles.UnassignDriver.NotFound", "The requested vehicle was not found."));
        }

        vehicle.UnassignDriver();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<VehicleResponse>.Success(VehicleMappings.Map(vehicle, null));
    }
}

public sealed class DeactivateVehicleHandler : IRequestHandler<DeactivateVehicleCommand, Result<VehicleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateVehicleHandler(ICurrentUserService currentUserService, IVehicleRepository vehicleRepository, IEmployeeRepository employeeRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _vehicleRepository = vehicleRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VehicleResponse>> Handle(DeactivateVehicleCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<VehicleResponse>.Failure(Error.Unauthorized("Vehicles.Deactivate.Unauthorized", "The current user is not authenticated."));
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(new VehicleId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (vehicle is null)
        {
            return Result<VehicleResponse>.Failure(Error.NotFound("Vehicles.Deactivate.NotFound", "The requested vehicle was not found."));
        }

        vehicle.Deactivate();
        var driver = vehicle.AssignedDriverEmployeeId is null ? null : await _employeeRepository.GetByIdAsync(vehicle.AssignedDriverEmployeeId, _currentUserService.CompanyId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<VehicleResponse>.Success(VehicleMappings.Map(vehicle, driver));
    }
}

public sealed class GetVehicleHandler : IRequestHandler<GetVehicleQuery, Result<VehicleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public GetVehicleHandler(ICurrentUserService currentUserService, IVehicleRepository vehicleRepository, IEmployeeRepository employeeRepository)
    {
        _currentUserService = currentUserService;
        _vehicleRepository = vehicleRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<VehicleResponse>> Handle(GetVehicleQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<VehicleResponse>.Failure(Error.Unauthorized("Vehicles.Get.Unauthorized", "The current user is not authenticated."));
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(new VehicleId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (vehicle is null)
        {
            return Result<VehicleResponse>.Failure(Error.NotFound("Vehicles.Get.NotFound", "The requested vehicle was not found."));
        }

        var driver = vehicle.AssignedDriverEmployeeId is null ? null : await _employeeRepository.GetByIdAsync(vehicle.AssignedDriverEmployeeId, _currentUserService.CompanyId, cancellationToken);
        return Result<VehicleResponse>.Success(VehicleMappings.Map(vehicle, driver));
    }
}

public sealed class ListVehiclesHandler : IRequestHandler<ListVehiclesQuery, Result<IReadOnlyList<VehicleResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public ListVehiclesHandler(ICurrentUserService currentUserService, IVehicleRepository vehicleRepository, IEmployeeRepository employeeRepository)
    {
        _currentUserService = currentUserService;
        _vehicleRepository = vehicleRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<IReadOnlyList<VehicleResponse>>> Handle(ListVehiclesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<IReadOnlyList<VehicleResponse>>.Failure(Error.Unauthorized("Vehicles.List.Unauthorized", "The current user is not authenticated."));
        }

        var vehicles = await _vehicleRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken);
        var employeeMap = new Dictionary<Guid, Employee>();

        foreach (var vehicle in vehicles.Where(x => x.AssignedDriverEmployeeId is not null))
        {
            if (!employeeMap.ContainsKey(vehicle.AssignedDriverEmployeeId!.Value))
            {
                var employee = await _employeeRepository.GetByIdAsync(vehicle.AssignedDriverEmployeeId!, _currentUserService.CompanyId, cancellationToken);
                if (employee is not null)
                {
                    employeeMap[employee.Id.Value] = employee;
                }
            }
        }

        return Result<IReadOnlyList<VehicleResponse>>.Success(
            vehicles.Select(vehicle => VehicleMappings.Map(
                vehicle,
                vehicle.AssignedDriverEmployeeId is not null && employeeMap.TryGetValue(vehicle.AssignedDriverEmployeeId.Value, out var driver) ? driver : null))
            .ToList());
    }
}

public sealed class CreateFleetLogHandler : IRequestHandler<CreateFleetLogCommand, Result<FleetLogResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IFleetLogRepository _fleetLogRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDriverProfileRepository _driverProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFleetLogHandler(ICurrentUserService currentUserService, IVehicleRepository vehicleRepository, IFleetLogRepository fleetLogRepository, IEmployeeRepository employeeRepository, IDriverProfileRepository driverProfileRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _vehicleRepository = vehicleRepository;
        _fleetLogRepository = fleetLogRepository;
        _employeeRepository = employeeRepository;
        _driverProfileRepository = driverProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FleetLogResponse>> Handle(CreateFleetLogCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null || _currentUserService.UserId is null)
        {
            return Result<FleetLogResponse>.Failure(Error.Unauthorized("FleetLogs.Create.Unauthorized", "The current user is not authenticated."));
        }

        if (!Enum.IsDefined(typeof(FleetLogType), request.Type))
        {
            return Result<FleetLogResponse>.Failure(Error.Validation("FleetLogs.Create.InvalidType", "The selected fleet log type is invalid."));
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(new VehicleId(request.VehicleId), _currentUserService.CompanyId, cancellationToken);
        if (vehicle is null)
        {
            return Result<FleetLogResponse>.Failure(Error.NotFound("FleetLogs.Create.VehicleNotFound", "The requested vehicle was not found."));
        }

        var employee = await VehicleRules.ValidateDriverAsync(request.PerformedByEmployeeId, _currentUserService.CompanyId, _employeeRepository, _driverProfileRepository, cancellationToken, allowInactive: true, requireDriverRole: false);
        if (request.PerformedByEmployeeId.HasValue && employee is null)
        {
            return Result<FleetLogResponse>.Failure(Error.Validation("FleetLogs.Create.InvalidEmployee", "The selected employee is invalid."));
        }

        FleetLog log;
        try
        {
            log = FleetLog.Create(_currentUserService.CompanyId, vehicle.Id, employee?.Id, (FleetLogType)request.Type, request.OccurredAt == default ? DateTime.UtcNow : request.OccurredAt, request.Odometer, request.FuelLiters, request.FuelCost, request.MaintenanceType, request.Description, request.Notes, _currentUserService.UserId);

            if ((FleetLogType)request.Type == FleetLogType.FuelLoad)
            {
                vehicle.RegisterFuelLoad(log.OccurredAt, request.Odometer);
            }
            else if ((FleetLogType)request.Type == FleetLogType.Maintenance)
            {
                vehicle.RegisterMaintenance(log.OccurredAt, request.Odometer);
            }
            else
            {
                vehicle.ApplyOdometer(request.Odometer);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<FleetLogResponse>.Failure(Error.Validation("FleetLogs.Create.InvalidInput", ex.Message));
        }

        await _fleetLogRepository.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<FleetLogResponse>.Success(VehicleMappings.Map(log, employee));
    }
}

public sealed class ListFleetLogsHandler : IRequestHandler<ListFleetLogsQuery, Result<IReadOnlyList<FleetLogResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IFleetLogRepository _fleetLogRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public ListFleetLogsHandler(ICurrentUserService currentUserService, IFleetLogRepository fleetLogRepository, IEmployeeRepository employeeRepository)
    {
        _currentUserService = currentUserService;
        _fleetLogRepository = fleetLogRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<IReadOnlyList<FleetLogResponse>>> Handle(ListFleetLogsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<IReadOnlyList<FleetLogResponse>>.Failure(Error.Unauthorized("FleetLogs.List.Unauthorized", "The current user is not authenticated."));
        }

        FleetLogType? type = null;
        if (request.Type.HasValue)
        {
            if (!Enum.IsDefined(typeof(FleetLogType), request.Type.Value))
            {
                return Result<IReadOnlyList<FleetLogResponse>>.Failure(Error.Validation("FleetLogs.List.InvalidType", "The selected fleet log type is invalid."));
            }

            type = (FleetLogType)request.Type.Value;
        }

        var logs = await _fleetLogRepository.ListByVehicleAsync(new VehicleId(request.VehicleId), _currentUserService.CompanyId, request.From, request.To, type, cancellationToken);
        var employees = new Dictionary<Guid, Employee>();

        foreach (var log in logs.Where(x => x.PerformedByEmployeeId is not null))
        {
            if (!employees.ContainsKey(log.PerformedByEmployeeId!.Value))
            {
                var employee = await _employeeRepository.GetByIdAsync(log.PerformedByEmployeeId!, _currentUserService.CompanyId, cancellationToken);
                if (employee is not null)
                {
                    employees[employee.Id.Value] = employee;
                }
            }
        }

        return Result<IReadOnlyList<FleetLogResponse>>.Success(
            logs.Select(log => VehicleMappings.Map(log, log.PerformedByEmployeeId is not null && employees.TryGetValue(log.PerformedByEmployeeId.Value, out var employee) ? employee : null)).ToList());
    }
}

internal static class VehicleMappings
{
    public static VehicleResponse Map(Vehicle vehicle, Employee? driver) =>
        new(
            vehicle.Id.Value,
            vehicle.BranchId?.Value,
            vehicle.AssignedDriverEmployeeId?.Value,
            driver?.FullName,
            vehicle.Plate,
            vehicle.Model,
            vehicle.Brand,
            vehicle.Year,
            (int)vehicle.FuelType,
            vehicle.FuelType.ToString(),
            vehicle.CurrentOdometer,
            vehicle.LastFuelLoadedAt,
            vehicle.LastMaintenanceAt,
            vehicle.Notes,
            vehicle.IsActive,
            vehicle.CreatedAt,
            vehicle.UpdatedAt);

    public static FleetLogResponse Map(FleetLog log, Employee? employee) =>
        new(
            log.Id.Value,
            log.VehicleId.Value,
            log.PerformedByEmployeeId?.Value,
            employee?.FullName,
            (int)log.Type,
            log.Type.ToString(),
            log.OccurredAt,
            log.Odometer,
            log.FuelLiters,
            log.FuelCost,
            log.MaintenanceType,
            log.Description,
            log.Notes,
            log.CreatedAt);
}

internal static class VehicleRules
{
    public static async Task<BranchId?> ResolveBranchAsync(Guid? branchId, eiti.Domain.Companies.CompanyId companyId, IBranchRepository branchRepository, CancellationToken cancellationToken)
    {
        if (!branchId.HasValue)
        {
            return null;
        }

        var branch = await branchRepository.GetByIdAsync(new BranchId(branchId.Value), companyId, cancellationToken);
        return branch?.Id;
    }

    public static async Task<Employee?> ValidateDriverAsync(
        Guid? employeeId,
        eiti.Domain.Companies.CompanyId companyId,
        IEmployeeRepository employeeRepository,
        IDriverProfileRepository driverProfileRepository,
        CancellationToken cancellationToken,
        bool allowInactive = false,
        bool requireDriverRole = true)
    {
        if (!employeeId.HasValue)
        {
            return null;
        }

        var employee = await employeeRepository.GetByIdAsync(new EmployeeId(employeeId.Value), companyId, cancellationToken);
        if (employee is null)
        {
            return null;
        }

        if (!allowInactive && !employee.IsActive)
        {
            return null;
        }

        if (requireDriverRole && employee.EmployeeRole != EmployeeRole.Driver)
        {
            return null;
        }

        if (requireDriverRole)
        {
            var profile = await driverProfileRepository.GetByEmployeeIdAsync(employee.Id, companyId, cancellationToken);
            if (profile is null)
            {
                return null;
            }
        }

        return employee;
    }
}
