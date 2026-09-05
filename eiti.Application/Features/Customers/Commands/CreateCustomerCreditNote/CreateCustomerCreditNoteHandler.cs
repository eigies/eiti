using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Common;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CreateCustomerCreditNote;

// Emite una nota de crédito al cliente: origina saldo a favor y lo imputa FIFO a sus ventas CC
// pendientes, igual que un cobro pero sin dinero. Queda visible en la caja del día como
// movimiento neutro (no altera el efectivo esperado).
public sealed class CreateCustomerCreditNoteHandler
    : IRequestHandler<CreateCustomerCreditNoteCommand, Result<CreateCustomerCreditNoteResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerCreditNoteRepository _creditNoteRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerCreditNoteHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        ICustomerCreditNoteRepository creditNoteRepository,
        ISaleRepository saleRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _creditNoteRepository = creditNoteRepository;
        _saleRepository = saleRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateCustomerCreditNoteResponse>> Handle(
        CreateCustomerCreditNoteCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<CreateCustomerCreditNoteResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var customer = await _customerRepository.GetByIdAsync(
            new CustomerId(command.CustomerId), companyId, cancellationToken);
        if (customer is null)
            return Result<CreateCustomerCreditNoteResponse>.Failure(
                CreateCustomerCreditNoteErrors.CustomerNotFound);

        // Venta asociada opcional: si viene, tiene que ser de este cliente y no estar anulada.
        if (command.SaleId.HasValue)
        {
            var sale = await _saleRepository.GetByIdAsync(
                new SaleId(command.SaleId.Value), cancellationToken);

            // GetByIdAsync no filtra por empresa: se chequea acá, si no una NC podría
            // apuntar a la venta de otra compañía.
            if (sale is null || sale.CompanyId != companyId || sale.CustomerId?.Value != command.CustomerId)
                return Result<CreateCustomerCreditNoteResponse>.Failure(
                    CreateCustomerCreditNoteErrors.SaleNotFound);

            if (sale.SaleStatus == SaleStatus.Cancel)
                return Result<CreateCustomerCreditNoteResponse>.Failure(
                    CreateCustomerCreditNoteErrors.SaleCancelled);
        }

        // Misma exigencia que un cobro: la NC es un hecho del turno y se ve en la sesión.
        var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
            _currentUserService, _cashDrawerRepository, _cashSessionRepository,
            userId, companyId, cancellationToken);
        if (resolve.Status != CashSessionResolveStatus.Resolved)
            return Result<CreateCustomerCreditNoteResponse>.Failure(
                resolve.Status == CashSessionResolveStatus.NoAssignedDrawer
                    ? CreateCustomerCreditNoteErrors.NoAssignedCashDrawer
                    : CreateCustomerCreditNoteErrors.NoCashSessionOpen);
        var session = resolve.Session!;

        if (BusinessDay.IsFromPreviousBusinessDay(session.OpenedAt))
            return Result<CreateCustomerCreditNoteResponse>.Failure(
                CreateCustomerCreditNoteErrors.CashSessionFromPreviousDay);

        // Numeración NCC-### por sucursal. Hereda el esquema por conteo de ventas y compras:
        // no es AFIP-válido y se resuelve en el proyecto fiscal, en los tres lugares a la vez.
        var count = await _creditNoteRepository.CountByBranchAsync(
            companyId.Value, session.BranchId.Value, cancellationToken);
        var code = $"NCC-{(count + 1).ToString().PadLeft(3, '0')}";

        var note = CustomerCreditNote.Create(
            companyId.Value,
            customer.Id.Value,
            session.BranchId.Value,
            code,
            command.Amount,
            command.Reason,
            command.Date,
            command.SaleId,
            userId.Value);

        await _creditNoteRepository.AddAsync(note, cancellationToken);

        // El saldo previo se guarda para poder decir cuanto aporto ESTA nota: el applicator
        // consume todo el saldo disponible, no solo el de la nota.
        var creditBefore = customer.CreditBalance;
        customer.AddCredit(note.Amount);

        // Imputación FIFO con back-link a la NC: sin creditNoteId, anularla no podría deshacerla.
        var application = await CustomerCreditApplicator.ApplyToPendingCcSalesAsync(
            customer, companyId, _saleRepository, cancellationToken,
            customerPaymentId: null, creditNoteId: note.Id);
        _customerRepository.Update(customer);

        // Confirmar stock de las ventas que pasaron a Paid. Reusa las entidades tracked del
        // applicator (ya traen Details) — sin re-fetch por venta.
        foreach (var sale in application.SalesNowPaidEntities)
        {
            foreach (var detail in sale.Details)
            {
                var stock = await _branchProductStockRepository.GetOrCreateAsync(
                    sale.BranchId,
                    detail.ProductId,
                    companyId,
                    cancellationToken);

                stock.ConfirmSaleOut(detail.Quantity);
                await _stockMovementRepository.AddAsync(
                    StockMovement.Create(
                        companyId,
                        sale.BranchId,
                        stock.ProductId,
                        stock.Id,
                        StockMovementType.SaleOut,
                        detail.Quantity,
                        "Sale",
                        sale.Id.Value,
                        "Stock confirmed as sold (CC paid via credit note).",
                        userId),
                    cancellationToken);
            }
        }

        // Movimiento neutro: visible en la sesión, ExpectedClosingAmount no se mueve.
        session.RegisterCustomerCreditNote(note.Amount, note.Id, note.Code, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Sobrante = lo de la nota que NO se imputo y quedo a favor. Se mide contra el saldo
        // previo: `note.Amount - imputado` daba negativo cuando ya habia saldo, porque las
        // imputaciones incluyen el credito viejo.
        var sobrante = Math.Max(0m, customer.CreditBalance - creditBefore);

        return Result<CreateCustomerCreditNoteResponse>.Success(new CreateCustomerCreditNoteResponse(
            note.Id,
            note.Code,
            note.Amount,
            customer.CreditBalance,
            application.Imputaciones,
            decimal.Round(sobrante, 2, MidpointRounding.AwayFromZero)));
    }
}
