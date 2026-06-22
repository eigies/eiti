using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Stock.Commands.ImportBranchPricing;

public sealed class ImportBranchPricingHandler
    : IRequestHandler<ImportBranchPricingCommand, Result<ImportBranchPricingResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ImportBranchPricingHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        IBranchRepository branchRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _branchRepository = branchRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ImportBranchPricingResponse>> Handle(
        ImportBranchPricingCommand request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<ImportBranchPricingResponse>.Failure(authCheck.Error);

        if (request.Rows.Count == 0)
            return Result<ImportBranchPricingResponse>.Failure(ImportBranchPricingErrors.RowsRequired);

        var companyId = _currentUserService.CompanyId!;

        var products = await _productRepository.GetByCompanyIdAsync(companyId, cancellationToken);
        var productsByCode = products.ToDictionary(p => NormalizeKey(p.Code), StringComparer.OrdinalIgnoreCase);

        var branches = await _branchRepository.ListByCompanyAsync(companyId, cancellationToken) ?? [];
        var branchesByName = branches.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);

        var updatedCount = 0;
        var skippedCount = 0;
        var rowResults = new List<ImportBranchPricingRowResponse>(request.Rows.Count);

        for (var index = 0; index < request.Rows.Count; index++)
        {
            var row = request.Rows[index];
            var rowNumber = index + 2;
            var code = (row.Code ?? string.Empty).Trim();
            var branchName = (row.BranchName ?? string.Empty).Trim();

            // Celda vacía en ambos overrides => no se toca nada (semántica "dejar como está").
            if (row.CostOverride is null && row.SalePriceOverride is null)
            {
                skippedCount++;
                rowResults.Add(SkipRow(rowNumber, code, branchName, "No hay valores para actualizar."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                rowResults.Add(ErrorRow(rowNumber, code, branchName, "El código del producto es obligatorio."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(branchName))
            {
                rowResults.Add(ErrorRow(rowNumber, code, branchName, "La sucursal es obligatoria."));
                continue;
            }

            if (!productsByCode.TryGetValue(NormalizeKey(code), out var product))
            {
                rowResults.Add(ErrorRow(rowNumber, code, branchName, $"No se encontró el producto '{code}'."));
                continue;
            }

            if (!branchesByName.TryGetValue(branchName, out var branch))
            {
                rowResults.Add(ErrorRow(rowNumber, code, branchName, $"No se encontró la sucursal '{branchName}'."));
                continue;
            }

            var branchAccess = _currentUserService.EnsureBranchAccess(branch.Id.Value);
            if (branchAccess.IsFailure)
            {
                rowResults.Add(ErrorRow(rowNumber, code, branchName, "No tenés acceso a esta sucursal."));
                continue;
            }

            var stock = await _branchProductStockRepository.GetOrCreateAsync(branch.Id, product.Id, companyId, cancellationToken);

            // Cada celda vacía conserva el override existente; solo se pisa lo que viene con valor.
            var newCost = row.CostOverride ?? stock.CostOverride;
            var newSalePrice = row.SalePriceOverride ?? stock.SalePriceOverride;

            try
            {
                stock.SetPricing(newCost, newSalePrice);
            }
            catch (ArgumentException)
            {
                rowResults.Add(ErrorRow(rowNumber, code, branchName, "El costo y el precio no pueden ser negativos."));
                continue;
            }

            updatedCount++;
            rowResults.Add(SuccessRow(rowNumber, product.Code, branch.Name, "Precio/costo actualizado."));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ImportBranchPricingResponse>.Success(
            new ImportBranchPricingResponse(
                request.Rows.Count,
                updatedCount,
                skippedCount,
                rowResults.Count(result => result.Action == "error"),
                rowResults));
    }

    private static string NormalizeKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static ImportBranchPricingRowResponse SuccessRow(int rowNumber, string code, string branchName, string message)
        => new(rowNumber, code, branchName, "updated", message);

    private static ImportBranchPricingRowResponse SkipRow(int rowNumber, string code, string branchName, string message)
        => new(rowNumber, code, branchName, "skipped", message);

    private static ImportBranchPricingRowResponse ErrorRow(int rowNumber, string code, string branchName, string message)
        => new(rowNumber, code, branchName, "error", message);
}
