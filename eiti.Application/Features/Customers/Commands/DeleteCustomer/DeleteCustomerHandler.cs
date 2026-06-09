using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.DeleteCustomer;

public sealed class DeleteCustomerHandler : IRequestHandler<DeleteCustomerCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCustomerHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(request.Id), companyId, cancellationToken);
        if (customer is null)
            return Result.Failure(DeleteCustomerErrors.NotFound);

        // Borrado físico solo si el cliente no tiene saldo CC ni ventas registradas.
        if (customer.CreditBalance != 0)
            return Result.Failure(DeleteCustomerErrors.HasBalance);

        if (await _customerRepository.IsReferencedAsync(customer.Id, companyId, cancellationToken))
            return Result.Failure(DeleteCustomerErrors.InUse);

        _customerRepository.Delete(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
