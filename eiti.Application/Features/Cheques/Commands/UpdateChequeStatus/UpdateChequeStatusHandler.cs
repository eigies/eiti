using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Cheques.Queries.GetChequeById;
using eiti.Domain.Cheques;
using MediatR;

namespace eiti.Application.Features.Cheques.Commands.UpdateChequeStatus;

public sealed class UpdateChequeStatusHandler : IRequestHandler<UpdateChequeStatusCommand, Result<ChequeDetailResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IChequeRepository _chequeRepository;
    private readonly IBankRepository _bankRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateChequeStatusHandler(
        ICurrentUserService currentUserService,
        IChequeRepository chequeRepository,
        IBankRepository bankRepository,
        ISaleRepository saleRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _chequeRepository = chequeRepository;
        _bankRepository = bankRepository;
        _saleRepository = saleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ChequeDetailResponse>> Handle(UpdateChequeStatusCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<ChequeDetailResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<ChequeDetailResponse>.Failure(UpdateChequeStatusErrors.Unauthorized);

        var companyId = _currentUserService.CompanyId!;
        var cheque = await _chequeRepository.GetByIdAsync(request.Id, companyId, cancellationToken);
        if (cheque is null)
            return Result<ChequeDetailResponse>.Failure(UpdateChequeStatusErrors.NotFound);

        if (!Enum.IsDefined(typeof(ChequeStatus), request.NewStatus))
            return Result<ChequeDetailResponse>.Failure(UpdateChequeStatusErrors.InvalidStatus);

        try
        {
            cheque.TransitionTo((ChequeStatus)request.NewStatus);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ChequeDetailResponse>.Failure(
                Error.Validation("Cheques.UpdateStatus.InvalidTransition", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var bank = await _bankRepository.GetByIdAsync(cheque.BankId, companyId, cancellationToken);
        var bankName = bank?.Name ?? "Unknown";

        string? saleCode = null;
        string saleType;

        if (cheque.SaleCcPaymentId.HasValue)
        {
            saleType = "CC";
            var ccPaymentSaleIds = await _saleRepository.GetSaleIdsByCcPaymentIdsAsync(
                [cheque.SaleCcPaymentId.Value], cancellationToken);
            if (ccPaymentSaleIds.TryGetValue(cheque.SaleCcPaymentId.Value, out var saleId))
            {
                var sales = await _saleRepository.GetByIdsAsync([saleId], cancellationToken);
                saleCode = sales.FirstOrDefault()?.Code;
            }
        }
        else if (cheque.SalePaymentSaleId.HasValue)
        {
            saleType = "Regular";
            var sales = await _saleRepository.GetByIdsAsync([cheque.SalePaymentSaleId.Value], cancellationToken);
            saleCode = sales.FirstOrDefault()?.Code;
        }
        else
        {
            saleType = "Unknown";
        }

        return Result<ChequeDetailResponse>.Success(new ChequeDetailResponse(
            cheque.Id,
            cheque.Numero,
            cheque.BankId,
            bankName,
            cheque.Titular,
            cheque.CuitDni,
            cheque.Monto,
            cheque.FechaEmision,
            cheque.FechaVencimiento,
            (int)cheque.Estado,
            cheque.Estado.ToString(),
            cheque.Notas,
            saleCode,
            saleType,
            cheque.CreatedAt,
            cheque.UpdatedAt));
    }
}
