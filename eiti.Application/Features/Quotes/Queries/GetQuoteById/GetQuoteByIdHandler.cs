using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using MediatR;

namespace eiti.Application.Features.Quotes.Queries.GetQuoteById;

public sealed class GetQuoteByIdHandler : IRequestHandler<GetQuoteByIdQuery, Result<QuoteDetailResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuoteRepository _quoteRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;

    public GetQuoteByIdHandler(
        ICurrentUserService currentUserService,
        IQuoteRepository quoteRepository,
        IBranchRepository branchRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository)
    {
        _currentUserService = currentUserService;
        _quoteRepository = quoteRepository;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<QuoteDetailResponse>> Handle(GetQuoteByIdQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<QuoteDetailResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var quote = await _quoteRepository.GetByIdAsync(new QuoteId(request.QuoteId), companyId, cancellationToken);
        if (quote is null)
        {
            return Result<QuoteDetailResponse>.Failure(GetQuoteByIdErrors.QuoteNotFound);
        }

        var branch = await _branchRepository.GetByIdAsync(quote.BranchId, companyId, cancellationToken);

        string? customerFullName = null;
        if (quote.CustomerId is not null)
        {
            var customer = await _customerRepository.GetByIdAsync(quote.CustomerId, companyId, cancellationToken);
            customerFullName = customer?.FullName;
        }

        var productIds = quote.Details.Select(detail => detail.ProductId).Distinct().ToList();
        var products = await _productRepository.GetByIdsAsync(productIds, companyId, cancellationToken);
        var productMap = products.ToDictionary(product => product.Id.Value, product => product);

        var now = DateTime.UtcNow;
        return Result<QuoteDetailResponse>.Success(new QuoteDetailResponse(
            quote.Id.Value,
            quote.Code,
            quote.BranchId.Value,
            branch?.Name ?? string.Empty,
            quote.CustomerId?.Value,
            customerFullName,
            quote.ProspectName,
            quote.ProspectContact,
            quote.GeneralDiscountPercent,
            quote.TotalAmount,
            quote.VatRate,
            quote.IncludesVat,
            quote.NetAmount,
            quote.VatAmount,
            quote.GrandTotal,
            quote.ExpiresAt,
            (int)quote.Status,
            quote.Status.ToString(),
            quote.IsExpired(now),
            quote.ConvertedSaleId,
            quote.CreatedAt,
            quote.Details.Select(detail => new QuoteDetailItemResponse(
                detail.ProductId.Value,
                productMap.TryGetValue(detail.ProductId.Value, out var product) ? product.Name : "Deleted product",
                productMap.TryGetValue(detail.ProductId.Value, out var product2) ? product2.Brand : "Unknown",
                detail.Quantity,
                detail.UnitPrice,
                detail.DiscountPercent,
                detail.LineTotal)).ToList()));
    }
}
