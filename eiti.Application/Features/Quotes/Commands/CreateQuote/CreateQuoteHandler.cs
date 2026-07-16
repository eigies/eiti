using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Branches;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using MediatR;

namespace eiti.Application.Features.Quotes.Commands.CreateQuote;

public sealed class CreateQuoteHandler : IRequestHandler<CreateQuoteCommand, Result<CreateQuoteResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateQuoteHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IQuoteRepository quoteRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateQuoteResponse>> Handle(CreateQuoteCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<CreateQuoteResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!.Value;

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), companyId, cancellationToken);
        if (branch is null)
        {
            return Result<CreateQuoteResponse>.Failure(CreateQuoteErrors.BranchNotFound);
        }

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _customerRepository.GetByIdAsync(new CustomerId(request.CustomerId.Value), companyId, cancellationToken);
            if (customer is null)
            {
                return Result<CreateQuoteResponse>.Failure(CreateQuoteErrors.CustomerNotFound);
            }
        }
        else if (string.IsNullOrWhiteSpace(request.ProspectName))
        {
            return Result<CreateQuoteResponse>.Failure(CreateQuoteErrors.InvalidCustomerOrProspect);
        }

        var productMap = new Dictionary<Guid, Product>();
        var quoteDetails = new List<QuoteDetail>();

        foreach (var detail in request.Details)
        {
            var product = await _productRepository.GetByIdAsync(new ProductId(detail.ProductId), companyId, cancellationToken);
            if (product is null)
            {
                return Result<CreateQuoteResponse>.Failure(CreateQuoteErrors.ProductNotFound);
            }

            productMap[product.Id.Value] = product;
            quoteDetails.Add(QuoteDetail.Create(product.Id, detail.Quantity, detail.UnitPrice, detail.DiscountPercent));
        }

        var branchQuoteCount = await _quoteRepository.CountByBranchAsync(branch.Id.Value, cancellationToken);
        var codePrefix = !string.IsNullOrWhiteSpace(branch.Code)
            ? branch.Code.ToUpper()
            : branch.Name.ToUpper()[..Math.Min(3, branch.Name.Length)];
        var quoteCode = $"PRES-{codePrefix}-{(branchQuoteCount + 1).ToString().PadLeft(3, '0')}";

        Quote quote;
        try
        {
            quote = Quote.Create(
                companyId,
                branch.Id,
                customer?.Id,
                request.ProspectName,
                request.ProspectContact,
                quoteDetails,
                request.GeneralDiscountPercent,
                request.ExpiresAt,
                userId,
                quoteCode);
        }
        catch (ArgumentException ex)
        {
            return Result<CreateQuoteResponse>.Failure(Error.Validation("Quotes.Create.InvalidInput", ex.Message));
        }

        await _quoteRepository.AddAsync(quote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateQuoteResponse>.Success(new CreateQuoteResponse(
            quote.Id.Value,
            quote.Code,
            quote.BranchId.Value,
            quote.CustomerId?.Value,
            customer?.FullName,
            quote.ProspectName,
            quote.ProspectContact,
            quote.GeneralDiscountPercent,
            quote.TotalAmount,
            quote.ExpiresAt,
            (int)quote.Status,
            quote.Status.ToString(),
            quote.CreatedAt,
            quote.Details.Select(detail => new CreateQuoteDetailItemResponse(
                detail.ProductId.Value,
                productMap[detail.ProductId.Value].Name,
                productMap[detail.ProductId.Value].Brand,
                detail.Quantity,
                detail.UnitPrice,
                detail.DiscountPercent,
                detail.LineTotal)).ToList()));
    }
}
