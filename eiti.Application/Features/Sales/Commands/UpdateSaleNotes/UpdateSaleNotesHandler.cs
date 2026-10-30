using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.UpdateSaleNotes;

public sealed class UpdateSaleNotesHandler : IRequestHandler<UpdateSaleNotesCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSaleNotesHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateSaleNotesCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var sale = await _saleRepository.GetByIdAsync(new SaleId(request.SaleId), cancellationToken);
        if (sale is null || sale.CompanyId != _currentUserService.CompanyId)
            return Result.Failure(UpdateSaleNotesErrors.NotFound);

        if (sale.SaleStatus == SaleStatus.Cancel)
            return Result.Failure(UpdateSaleNotesErrors.Cancelled);

        sale.SetNotes(request.Notes);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
