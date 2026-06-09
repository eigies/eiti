using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.ProductCategories.Commands.CreateProductCategory;

public sealed record CreateProductCategoryCommand(
    string Name
) : IRequest<Result<CreateProductCategoryResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CategoriesManage];
}

public sealed record CreateProductCategoryResponse(Guid Id, string Name);
