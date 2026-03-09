using eiti.Api.Extensions;
using eiti.Application.Features.Products.Commands.CreateProduct;
using eiti.Application.Features.Products.Commands.DeleteProduct;
using eiti.Application.Features.Products.Commands.UpdateProduct;
using eiti.Application.Features.Products.Queries.ListPagedProducts;
using eiti.Application.Features.Products.Queries.ListProducts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly ISender _sender;

    public ProductsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> ListProducts(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListProductsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("paged")]
    public async Task<IActionResult> ListPagedProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ListPagedProductsQuery(page, pageSize), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProduct(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteProductCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}
