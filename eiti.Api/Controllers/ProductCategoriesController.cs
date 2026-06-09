using eiti.Api.Extensions;
using eiti.Application.Features.ProductCategories.Commands.CreateProductCategory;
using eiti.Application.Features.ProductCategories.Commands.DeleteProductCategory;
using eiti.Application.Features.ProductCategories.Commands.UpdateProductCategory;
using eiti.Application.Features.ProductCategories.Queries.ListProductCategories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/product-categories")]
[Authorize]
public sealed class ProductCategoriesController : ControllerBase
{
    private readonly ISender _sender;

    public ProductCategoriesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListProductCategoriesQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCategoryCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCategoryCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteProductCategoryCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}
