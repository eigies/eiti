using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Employees;
using eiti.Domain.Sales;
using eiti.Domain.Transport;
using eiti.Domain.Vehicles;
using MediatR;

namespace eiti.Application.Features.SaleTransport;

public sealed record SaleTransportResponse(
    Guid Id,
    Guid SaleId,
    Guid DriverEmployeeId,
    string DriverFullName,
    Guid VehicleId,
    string VehiclePlate,
    int Status,
    string StatusName,
    DateTime AssignedAt,
    DateTime? DispatchedAt,
    DateTime? DeliveredAt,
    string? Notes,
    DateTime? UpdatedAt);

public sealed record CreateSaleTransportCommand(Guid SaleId, Guid DriverEmployeeId, Guid VehicleId, string? Notes) : IRequest<Result<SaleTransportResponse>>;
public sealed record UpdateSaleTransportCommand(Guid SaleId, Guid DriverEmployeeId, Guid VehicleId, string? Notes) : IRequest<Result<SaleTransportResponse>>;
public sealed record UpdateSaleTransportStatusCommand(Guid SaleId, int Status) : IRequest<Result<SaleTransportResponse>>;
public sealed record DeleteSaleTransportCommand(Guid SaleId) : IRequest<Result>;
public sealed record GetSaleTransportQuery(Guid SaleId) : IRequest<Result<SaleTransportResponse>>;

public sealed class CreateSaleTransportHandler : IRequestHandler<CreateSaleTransportCommand, Result<SaleTransportResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleTransportAssignmentRepository _saleTransportAssignmentRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDriverProfileRepository _driverProfileRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSaleTransportHandler(ICurrentUserService currentUserService, ISaleRepository saleRepository, ISaleTransportAssignmentRepository saleTransportAssignmentRepository, IEmployeeRepository employeeRepository, IDriverProfileRepository driverProfileRepository, IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _saleTransportAssignmentRepository = saleTransportAssignmentRepository;
        _employeeRepository = employeeRepository;
        _driverProfileRepository = driverProfileRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SaleTransportResponse>> Handle(CreateSaleTransportCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<SaleTransportResponse>.Failure(authCheck.Error);

        var sale = await _saleRepository.GetByIdAsync(new SaleId(request.SaleId), cancellationToken);
        if (sale is null || sale.CompanyId != _currentUserService.CompanyId)
        {
            return Result<SaleTransportResponse>.Failure(Error.NotFound("SaleTransport.Create.SaleNotFound", "The requested sale was not found."));
        }

        var existing = await _saleTransportAssignmentRepository.GetBySaleIdAsync(sale.Id, _currentUserService.CompanyId, cancellationToken);
        if (existing is not null && existing.Status != SaleTransportStatus.Cancelled)
        {
            return Result<SaleTransportResponse>.Failure(Error.Conflict("SaleTransport.Create.AlreadyExists", "This sale already has a transport assignment."));
        }

        var validation = await SaleTransportRules.ValidateForAssignmentAsync(sale, request.DriverEmployeeId, request.VehicleId, _currentUserService.CompanyId, _employeeRepository, _driverProfileRepository, _vehicleRepository, cancellationToken);
        if (validation.Error is not null)
        {
            return Result<SaleTransportResponse>.Failure(validation.Error);
        }

        SaleTransportAssignment assignment;
        if (existing is not null)
        {
            // Reuse the cancelled record to avoid violating the unique index on SaleId.
            existing.Reassign(validation.Driver!.Id, validation.Vehicle!.Id, request.Notes);
            assignment = existing;
        }
        else
        {
            assignment = SaleTransportAssignment.Create(sale.Id, _currentUserService.CompanyId, validation.Driver!.Id, validation.Vehicle!.Id, request.Notes, _currentUserService.UserId);
            await _saleTransportAssignmentRepository.AddAsync(assignment, cancellationToken);
        }

        sale.AssignTransport(assignment.Id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<SaleTransportResponse>.Success(SaleTransportMappings.Map(assignment, validation.Driver, validation.Vehicle));
    }
}

public sealed class UpdateSaleTransportHandler : IRequestHandler<UpdateSaleTransportCommand, Result<SaleTransportResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleTransportAssignmentRepository _saleTransportAssignmentRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDriverProfileRepository _driverProfileRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSaleTransportHandler(ICurrentUserService currentUserService, ISaleRepository saleRepository, ISaleTransportAssignmentRepository saleTransportAssignmentRepository, IEmployeeRepository employeeRepository, IDriverProfileRepository driverProfileRepository, IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _saleTransportAssignmentRepository = saleTransportAssignmentRepository;
        _employeeRepository = employeeRepository;
        _driverProfileRepository = driverProfileRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SaleTransportResponse>> Handle(UpdateSaleTransportCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<SaleTransportResponse>.Failure(authCheck.Error);

        var sale = await _saleRepository.GetByIdAsync(new SaleId(request.SaleId), cancellationToken);
        var assignment = sale is null ? null : await _saleTransportAssignmentRepository.GetBySaleIdAsync(sale.Id, _currentUserService.CompanyId, cancellationToken);
        if (sale is null || sale.CompanyId != _currentUserService.CompanyId || assignment is null)
        {
            return Result<SaleTransportResponse>.Failure(Error.NotFound("SaleTransport.Update.NotFound", "The requested transport assignment was not found."));
        }

        var validation = await SaleTransportRules.ValidateForAssignmentAsync(sale, request.DriverEmployeeId, request.VehicleId, _currentUserService.CompanyId, _employeeRepository, _driverProfileRepository, _vehicleRepository, cancellationToken);
        if (validation.Error is not null)
        {
            return Result<SaleTransportResponse>.Failure(validation.Error);
        }

        try
        {
            assignment.UpdateAssignment(validation.Driver!.Id, validation.Vehicle!.Id, request.Notes);
        }
        catch (InvalidOperationException ex)
        {
            return Result<SaleTransportResponse>.Failure(Error.Conflict("SaleTransport.Update.InvalidState", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<SaleTransportResponse>.Success(SaleTransportMappings.Map(assignment, validation.Driver, validation.Vehicle));
    }
}

public sealed class UpdateSaleTransportStatusHandler : IRequestHandler<UpdateSaleTransportStatusCommand, Result<SaleTransportResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleTransportAssignmentRepository _saleTransportAssignmentRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSaleTransportStatusHandler(ICurrentUserService currentUserService, ISaleRepository saleRepository, ISaleTransportAssignmentRepository saleTransportAssignmentRepository, IEmployeeRepository employeeRepository, IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _saleTransportAssignmentRepository = saleTransportAssignmentRepository;
        _employeeRepository = employeeRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SaleTransportResponse>> Handle(UpdateSaleTransportStatusCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<SaleTransportResponse>.Failure(authCheck.Error);

        if (!Enum.IsDefined(typeof(SaleTransportStatus), request.Status))
        {
            return Result<SaleTransportResponse>.Failure(Error.Validation("SaleTransport.UpdateStatus.InvalidStatus", "The selected transport status is invalid."));
        }

        var sale = await _saleRepository.GetByIdAsync(new SaleId(request.SaleId), cancellationToken);
        var assignment = sale is null ? null : await _saleTransportAssignmentRepository.GetBySaleIdAsync(sale.Id, _currentUserService.CompanyId, cancellationToken);
        if (sale is null || sale.CompanyId != _currentUserService.CompanyId || assignment is null)
        {
            return Result<SaleTransportResponse>.Failure(Error.NotFound("SaleTransport.UpdateStatus.NotFound", "The requested transport assignment was not found."));
        }

        try
        {
            switch ((SaleTransportStatus)request.Status)
            {
                case SaleTransportStatus.Assigned:
                    return Result<SaleTransportResponse>.Failure(Error.Validation("SaleTransport.UpdateStatus.InvalidTransition", "Transport cannot be moved back to Assigned."));
                case SaleTransportStatus.InTransit:
                    assignment.MarkInTransit();
                    break;
                case SaleTransportStatus.Delivered:
                    if (sale.SaleStatus != SaleStatus.Paid)
                    {
                        return Result<SaleTransportResponse>.Failure(Error.Conflict("SaleTransport.UpdateStatus.RequiresPaidSale", "Only paid sales can be marked as delivered."));
                    }

                    assignment.MarkDelivered();
                    break;
                case SaleTransportStatus.Cancelled:
                    assignment.Cancel();
                    break;
            }
        }
        catch (InvalidOperationException ex)
        {
            return Result<SaleTransportResponse>.Failure(Error.Conflict("SaleTransport.UpdateStatus.InvalidState", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var driver = await _employeeRepository.GetByIdAsync(assignment.DriverEmployeeId, _currentUserService.CompanyId, cancellationToken);
        var vehicle = await _vehicleRepository.GetByIdAsync(assignment.VehicleId, _currentUserService.CompanyId, cancellationToken);
        return Result<SaleTransportResponse>.Success(SaleTransportMappings.Map(assignment, driver!, vehicle!));
    }
}

public sealed class DeleteSaleTransportHandler : IRequestHandler<DeleteSaleTransportCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleTransportAssignmentRepository _saleTransportAssignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSaleTransportHandler(ICurrentUserService currentUserService, ISaleRepository saleRepository, ISaleTransportAssignmentRepository saleTransportAssignmentRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _saleTransportAssignmentRepository = saleTransportAssignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteSaleTransportCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var sale = await _saleRepository.GetByIdAsync(new SaleId(request.SaleId), cancellationToken);
        var assignment = sale is null ? null : await _saleTransportAssignmentRepository.GetBySaleIdAsync(sale.Id, _currentUserService.CompanyId, cancellationToken);
        if (sale is null || sale.CompanyId != _currentUserService.CompanyId || assignment is null)
        {
            return Result.Failure(Error.NotFound("SaleTransport.Delete.NotFound", "The requested transport assignment was not found."));
        }

        if (assignment.Status != SaleTransportStatus.Assigned)
        {
            return Result.Failure(Error.Conflict("SaleTransport.Delete.InvalidState", "Only assigned transports can be removed."));
        }

        assignment.Cancel();
        sale.ClearTransportAssignment();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class GetSaleTransportHandler : IRequestHandler<GetSaleTransportQuery, Result<SaleTransportResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleTransportAssignmentRepository _saleTransportAssignmentRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IVehicleRepository _vehicleRepository;

    public GetSaleTransportHandler(ICurrentUserService currentUserService, ISaleRepository saleRepository, ISaleTransportAssignmentRepository saleTransportAssignmentRepository, IEmployeeRepository employeeRepository, IVehicleRepository vehicleRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _saleTransportAssignmentRepository = saleTransportAssignmentRepository;
        _employeeRepository = employeeRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<SaleTransportResponse>> Handle(GetSaleTransportQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<SaleTransportResponse>.Failure(authCheck.Error);

        var sale = await _saleRepository.GetByIdAsync(new SaleId(request.SaleId), cancellationToken);
        var assignment = sale is null ? null : await _saleTransportAssignmentRepository.GetBySaleIdAsync(sale.Id, _currentUserService.CompanyId, cancellationToken);
        if (sale is null || sale.CompanyId != _currentUserService.CompanyId || assignment is null)
        {
            return Result<SaleTransportResponse>.Failure(Error.NotFound("SaleTransport.Get.NotFound", "The requested transport assignment was not found."));
        }

        var driver = await _employeeRepository.GetByIdAsync(assignment.DriverEmployeeId, _currentUserService.CompanyId, cancellationToken);
        var vehicle = await _vehicleRepository.GetByIdAsync(assignment.VehicleId, _currentUserService.CompanyId, cancellationToken);
        return Result<SaleTransportResponse>.Success(SaleTransportMappings.Map(assignment, driver!, vehicle!));
    }
}

internal static class SaleTransportRules
{
    public static async Task<(Error? Error, Employee? Driver, Vehicle? Vehicle)> ValidateForAssignmentAsync(
        Sale sale,
        Guid driverEmployeeId,
        Guid vehicleId,
        eiti.Domain.Companies.CompanyId companyId,
        IEmployeeRepository employeeRepository,
        IDriverProfileRepository driverProfileRepository,
        IVehicleRepository vehicleRepository,
        CancellationToken cancellationToken)
    {
        if (!sale.HasDelivery)
        {
            return (Error.Validation("SaleTransport.InvalidSale", "This sale is not marked for delivery."), null, null);
        }

        if (sale.SaleStatus == SaleStatus.Cancel)
        {
            return (Error.Conflict("SaleTransport.InvalidSaleState", "Cancelled sales cannot receive transport assignments."), null, null);
        }

        var driver = await employeeRepository.GetByIdAsync(new EmployeeId(driverEmployeeId), companyId, cancellationToken);
        if (driver is null || !driver.IsActive || driver.EmployeeRole != EmployeeRole.Driver)
        {
            return (Error.Validation("SaleTransport.InvalidDriver", "The selected driver is invalid."), null, null);
        }

        var profile = await driverProfileRepository.GetByEmployeeIdAsync(driver.Id, companyId, cancellationToken);
        if (profile is null || profile.IsExpired(DateTime.UtcNow))
        {
            return (Error.Validation("SaleTransport.InvalidDriverProfile", "The selected driver is missing a valid license."), null, null);
        }

        var vehicle = await vehicleRepository.GetByIdAsync(new VehicleId(vehicleId), companyId, cancellationToken);
        if (vehicle is null || !vehicle.IsActive)
        {
            return (Error.Validation("SaleTransport.InvalidVehicle", "The selected vehicle is invalid."), null, null);
        }

        if (vehicle.AssignedDriverEmployeeId is not null && vehicle.AssignedDriverEmployeeId != driver.Id)
        {
            return (Error.Conflict("SaleTransport.DriverVehicleMismatch", "The selected vehicle is assigned to a different driver."), null, null);
        }

        return (null, driver, vehicle);
    }
}

internal static class SaleTransportMappings
{
    public static SaleTransportResponse Map(SaleTransportAssignment assignment, Employee driver, Vehicle vehicle) =>
        new(
            assignment.Id.Value,
            assignment.SaleId.Value,
            driver.Id.Value,
            driver.FullName,
            vehicle.Id.Value,
            vehicle.Plate,
            (int)assignment.Status,
            assignment.Status.ToString(),
            assignment.AssignedAt,
            assignment.DispatchedAt,
            assignment.DeliveredAt,
            assignment.Notes,
            assignment.UpdatedAt);
}
