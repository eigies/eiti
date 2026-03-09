using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.DeleteSale;

public sealed class DeleteSaleHandler : IRequestHandler<DeleteSaleCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSaleHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        DeleteSaleCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result.Failure(
                Error.Unauthorized(
                    "Sales.Delete.Unauthorized",
                    "The current user is not authenticated."));
        }

        var sale = await _saleRepository.GetByIdAsync(new SaleId(request.Id), cancellationToken);

        if (sale is null || sale.CompanyId != _currentUserService.CompanyId)
        {
            return Result.Failure(
                Error.NotFound(
                    "Sales.Delete.NotFound",
                    "The requested sale was not found."));
        }

        if (sale.SaleStatus != SaleStatus.Cancel)
        {
            return Result.Failure(
                Error.Conflict(
                    "Sales.Delete.NotCancelled",
                    "Only sales with Cancel status can be deleted."));
        }

        _saleRepository.Remove(sale);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
