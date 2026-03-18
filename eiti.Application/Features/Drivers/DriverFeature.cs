using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Employees;
using MediatR;

namespace eiti.Application.Features.Drivers;

public sealed record DriverResponse(
    Guid EmployeeId,
    string FullName,
    bool IsActive,
    string LicenseNumber,
    string? LicenseCategory,
    DateTime? LicenseExpiresAt,
    bool IsLicenseExpired,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record UpsertDriverProfileCommand(
    Guid EmployeeId,
    string LicenseNumber,
    string? LicenseCategory,
    DateTime? LicenseExpiresAt,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Notes) : IRequest<Result<DriverResponse>>;

public sealed record GetDriverQuery(Guid EmployeeId) : IRequest<Result<DriverResponse>>;
public sealed record ListDriversQuery() : IRequest<Result<IReadOnlyList<DriverResponse>>>;

public sealed class UpsertDriverProfileHandler : IRequestHandler<UpsertDriverProfileCommand, Result<DriverResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDriverProfileRepository _driverProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpsertDriverProfileHandler(ICurrentUserService currentUserService, IEmployeeRepository employeeRepository, IDriverProfileRepository driverProfileRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _driverProfileRepository = driverProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DriverResponse>> Handle(UpsertDriverProfileCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<DriverResponse>.Failure(authCheck.Error);

        var employee = await _employeeRepository.GetByIdAsync(new EmployeeId(request.EmployeeId), _currentUserService.CompanyId, cancellationToken);
        if (employee is null)
        {
            return Result<DriverResponse>.Failure(Error.NotFound("Drivers.Upsert.EmployeeNotFound", "The requested employee was not found."));
        }

        if (employee.EmployeeRole != EmployeeRole.Driver)
        {
            return Result<DriverResponse>.Failure(Error.Validation("Drivers.Upsert.InvalidEmployeeRole", "Only employees with the Driver role can have a driver profile."));
        }

        var existing = await _driverProfileRepository.GetByEmployeeIdAsync(employee.Id, _currentUserService.CompanyId, cancellationToken);

        if (await _driverProfileRepository.LicenseExistsAsync(_currentUserService.CompanyId, request.LicenseNumber.Trim(), employee.Id, cancellationToken))
        {
            return Result<DriverResponse>.Failure(Error.Conflict("Drivers.Upsert.LicenseExists", "A driver with the same license already exists."));
        }

        try
        {
            if (existing is null)
            {
                existing = DriverProfile.Create(employee.Id, _currentUserService.CompanyId, request.LicenseNumber, request.LicenseCategory, request.LicenseExpiresAt, request.EmergencyContactName, request.EmergencyContactPhone, request.Notes);
                await _driverProfileRepository.AddAsync(existing, cancellationToken);
            }
            else
            {
                existing.Update(request.LicenseNumber, request.LicenseCategory, request.LicenseExpiresAt, request.EmergencyContactName, request.EmergencyContactPhone, request.Notes);
            }
        }
        catch (ArgumentException ex)
        {
            return Result<DriverResponse>.Failure(Error.Validation("Drivers.Upsert.InvalidInput", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<DriverResponse>.Success(DriverMappings.Map(employee, existing));
    }
}

public sealed class GetDriverHandler : IRequestHandler<GetDriverQuery, Result<DriverResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDriverProfileRepository _driverProfileRepository;

    public GetDriverHandler(ICurrentUserService currentUserService, IEmployeeRepository employeeRepository, IDriverProfileRepository driverProfileRepository)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _driverProfileRepository = driverProfileRepository;
    }

    public async Task<Result<DriverResponse>> Handle(GetDriverQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<DriverResponse>.Failure(authCheck.Error);

        var employee = await _employeeRepository.GetByIdAsync(new EmployeeId(request.EmployeeId), _currentUserService.CompanyId, cancellationToken);
        var profile = employee is null ? null : await _driverProfileRepository.GetByEmployeeIdAsync(employee.Id, _currentUserService.CompanyId, cancellationToken);

        return employee is null || profile is null
            ? Result<DriverResponse>.Failure(Error.NotFound("Drivers.Get.NotFound", "The requested driver was not found."))
            : Result<DriverResponse>.Success(DriverMappings.Map(employee, profile));
    }
}

public sealed class ListDriversHandler : IRequestHandler<ListDriversQuery, Result<IReadOnlyList<DriverResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDriverProfileRepository _driverProfileRepository;

    public ListDriversHandler(ICurrentUserService currentUserService, IEmployeeRepository employeeRepository, IDriverProfileRepository driverProfileRepository)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _driverProfileRepository = driverProfileRepository;
    }

    public async Task<Result<IReadOnlyList<DriverResponse>>> Handle(ListDriversQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<DriverResponse>>.Failure(authCheck.Error);

        var employees = await _employeeRepository.ListDriversByCompanyAsync(_currentUserService.CompanyId, cancellationToken);
        var profiles = await _driverProfileRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken);
        var profilesByEmployee = profiles.ToDictionary(x => x.EmployeeId.Value, x => x);

        var result = employees
            .Where(employee => profilesByEmployee.ContainsKey(employee.Id.Value))
            .Select(employee => DriverMappings.Map(employee, profilesByEmployee[employee.Id.Value]))
            .ToList();

        return Result<IReadOnlyList<DriverResponse>>.Success(result);
    }
}

internal static class DriverMappings
{
    public static DriverResponse Map(Employee employee, DriverProfile profile) =>
        new(
            employee.Id.Value,
            employee.FullName,
            employee.IsActive,
            profile.LicenseNumber,
            profile.LicenseCategory,
            profile.LicenseExpiresAt,
            profile.IsExpired(DateTime.UtcNow),
            profile.EmergencyContactName,
            profile.EmergencyContactPhone,
            profile.Notes,
            profile.CreatedAt,
            profile.UpdatedAt);
}
