using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Branches;
using eiti.Domain.Employees;
using MediatR;

namespace eiti.Application.Features.Employees;

public sealed record EmployeeResponse(
    Guid Id,
    Guid? BranchId,
    string FirstName,
    string LastName,
    string FullName,
    string? DocumentNumber,
    string? Phone,
    string? Email,
    int EmployeeRole,
    string EmployeeRoleName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateEmployeeCommand(
    Guid? BranchId,
    string FirstName,
    string LastName,
    string? DocumentNumber,
    string? Phone,
    string? Email,
    int EmployeeRole) : IRequest<Result<EmployeeResponse>>;

public sealed record UpdateEmployeeCommand(
    Guid Id,
    Guid? BranchId,
    string FirstName,
    string LastName,
    string? DocumentNumber,
    string? Phone,
    string? Email,
    int EmployeeRole) : IRequest<Result<EmployeeResponse>>;

public sealed record DeactivateEmployeeCommand(Guid Id) : IRequest<Result<EmployeeResponse>>;
public sealed record GetEmployeeQuery(Guid Id) : IRequest<Result<EmployeeResponse>>;
public sealed record ListEmployeesQuery() : IRequest<Result<IReadOnlyList<EmployeeResponse>>>;
public sealed record ListDriverEmployeesQuery() : IRequest<Result<IReadOnlyList<EmployeeResponse>>>;

public sealed class CreateEmployeeHandler : IRequestHandler<CreateEmployeeCommand, Result<EmployeeResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateEmployeeHandler(ICurrentUserService currentUserService, IEmployeeRepository employeeRepository, IBranchRepository branchRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _branchRepository = branchRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeResponse>> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<EmployeeResponse>.Failure(authCheck.Error);

        if (!Enum.IsDefined(typeof(EmployeeRole), request.EmployeeRole))
        {
            return Result<EmployeeResponse>.Failure(Error.Validation("Employees.Create.InvalidRole", "The selected employee role is invalid."));
        }

        var branchId = await ResolveBranchAsync(request.BranchId, cancellationToken);
        if (request.BranchId.HasValue && branchId is null)
        {
            return Result<EmployeeResponse>.Failure(Error.NotFound("Employees.Create.BranchNotFound", "The requested branch was not found."));
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentNumber) &&
            await _employeeRepository.DocumentExistsAsync(_currentUserService.CompanyId, request.DocumentNumber.Trim(), null, cancellationToken))
        {
            return Result<EmployeeResponse>.Failure(Error.Conflict("Employees.Create.DocumentExists", "An employee with the same document already exists."));
        }

        Employee employee;
        try
        {
            employee = Employee.Create(_currentUserService.CompanyId, branchId, request.FirstName, request.LastName, request.DocumentNumber, request.Phone, request.Email, (EmployeeRole)request.EmployeeRole);
        }
        catch (ArgumentException ex)
        {
            return Result<EmployeeResponse>.Failure(Error.Validation("Employees.Create.InvalidInput", ex.Message));
        }

        await _employeeRepository.AddAsync(employee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EmployeeResponse>.Success(EmployeeMappings.Map(employee));
    }

    private async Task<BranchId?> ResolveBranchAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        if (!branchId.HasValue || _currentUserService.CompanyId is null)
        {
            return null;
        }

        var branch = await _branchRepository.GetByIdAsync(new BranchId(branchId.Value), _currentUserService.CompanyId, cancellationToken);
        return branch?.Id;
    }
}

public sealed class UpdateEmployeeHandler : IRequestHandler<UpdateEmployeeCommand, Result<EmployeeResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateEmployeeHandler(ICurrentUserService currentUserService, IEmployeeRepository employeeRepository, IBranchRepository branchRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _branchRepository = branchRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeResponse>> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<EmployeeResponse>.Failure(authCheck.Error);

        var employee = await _employeeRepository.GetByIdAsync(new EmployeeId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeResponse>.Failure(Error.NotFound("Employees.Update.NotFound", "The requested employee was not found."));
        }

        if (!Enum.IsDefined(typeof(EmployeeRole), request.EmployeeRole))
        {
            return Result<EmployeeResponse>.Failure(Error.Validation("Employees.Update.InvalidRole", "The selected employee role is invalid."));
        }

        BranchId? branchId = null;
        if (request.BranchId.HasValue)
        {
            var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId.Value), _currentUserService.CompanyId, cancellationToken);
            if (branch is null)
            {
                return Result<EmployeeResponse>.Failure(Error.NotFound("Employees.Update.BranchNotFound", "The requested branch was not found."));
            }

            branchId = branch.Id;
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentNumber) &&
            await _employeeRepository.DocumentExistsAsync(_currentUserService.CompanyId, request.DocumentNumber.Trim(), employee.Id, cancellationToken))
        {
            return Result<EmployeeResponse>.Failure(Error.Conflict("Employees.Update.DocumentExists", "An employee with the same document already exists."));
        }

        try
        {
            employee.Update(branchId, request.FirstName, request.LastName, request.DocumentNumber, request.Phone, request.Email, (EmployeeRole)request.EmployeeRole);
        }
        catch (ArgumentException ex)
        {
            return Result<EmployeeResponse>.Failure(Error.Validation("Employees.Update.InvalidInput", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EmployeeResponse>.Success(EmployeeMappings.Map(employee));
    }
}

public sealed class DeactivateEmployeeHandler : IRequestHandler<DeactivateEmployeeCommand, Result<EmployeeResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateEmployeeHandler(ICurrentUserService currentUserService, IEmployeeRepository employeeRepository, IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeResponse>> Handle(DeactivateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<EmployeeResponse>.Failure(authCheck.Error);

        var employee = await _employeeRepository.GetByIdAsync(new EmployeeId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeResponse>.Failure(Error.NotFound("Employees.Deactivate.NotFound", "The requested employee was not found."));
        }

        employee.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EmployeeResponse>.Success(EmployeeMappings.Map(employee));
    }
}

public sealed class GetEmployeeHandler : IRequestHandler<GetEmployeeQuery, Result<EmployeeResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;

    public GetEmployeeHandler(ICurrentUserService currentUserService, IEmployeeRepository employeeRepository)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<EmployeeResponse>> Handle(GetEmployeeQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<EmployeeResponse>.Failure(authCheck.Error);

        var employee = await _employeeRepository.GetByIdAsync(new EmployeeId(request.Id), _currentUserService.CompanyId, cancellationToken);
        return employee is null
            ? Result<EmployeeResponse>.Failure(Error.NotFound("Employees.Get.NotFound", "The requested employee was not found."))
            : Result<EmployeeResponse>.Success(EmployeeMappings.Map(employee));
    }
}

public sealed class ListEmployeesHandler : IRequestHandler<ListEmployeesQuery, Result<IReadOnlyList<EmployeeResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;

    public ListEmployeesHandler(ICurrentUserService currentUserService, IEmployeeRepository employeeRepository)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<IReadOnlyList<EmployeeResponse>>> Handle(ListEmployeesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<EmployeeResponse>>.Failure(authCheck.Error);

        var items = await _employeeRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken);
        return Result<IReadOnlyList<EmployeeResponse>>.Success(items.Select(EmployeeMappings.Map).ToList());
    }
}

public sealed class ListDriverEmployeesHandler : IRequestHandler<ListDriverEmployeesQuery, Result<IReadOnlyList<EmployeeResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;

    public ListDriverEmployeesHandler(ICurrentUserService currentUserService, IEmployeeRepository employeeRepository)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<IReadOnlyList<EmployeeResponse>>> Handle(ListDriverEmployeesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<EmployeeResponse>>.Failure(authCheck.Error);

        var items = await _employeeRepository.ListDriversByCompanyAsync(_currentUserService.CompanyId, cancellationToken);
        return Result<IReadOnlyList<EmployeeResponse>>.Success(items.Select(EmployeeMappings.Map).ToList());
    }
}

internal static class EmployeeMappings
{
    public static EmployeeResponse Map(Employee employee) =>
        new(
            employee.Id.Value,
            employee.BranchId?.Value,
            employee.FirstName,
            employee.LastName,
            employee.FullName,
            employee.DocumentNumber,
            employee.Phone,
            employee.Email,
            (int)employee.EmployeeRole,
            employee.EmployeeRole.ToString(),
            employee.IsActive,
            employee.CreatedAt,
            employee.UpdatedAt);
}
