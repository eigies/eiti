using eiti.Application.Common.Authorization;
using eiti.Application.Features.Auth.Common;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using FluentAssertions;

namespace eiti.Tests.Unit;

public sealed class AuthenticationMapperTests
{
    private static User CreateUserWithPermissions(params string[] permissionCodes)
    {
        var companyId = CompanyId.New();
        var profile = AccessProfile.Create(companyId, "Test", "Test profile", permissionCodes);

        return User.Create(
            Username.Create("testuser"),
            eiti.Domain.Customers.Email.Create("test@example.com"),
            PasswordHash.Create("hashed"),
            companyId,
            profile);
    }

    [Fact]
    public void MapPermissions_ShouldReturnPermissionsSorted()
    {
        var user = CreateUserWithPermissions(PermissionCodes.UsersManage, PermissionCodes.SalesAccess);

        var permissions = AuthenticationMapper.MapPermissions(user);

        permissions.Should().NotBeEmpty();
        permissions.Should().BeInAscendingOrder();
    }

    [Fact]
    public void MapPermissions_ShouldReturnCorrectPermissions_ForSellerRole()
    {
        var companyId = CompanyId.New();
        var profile = AccessProfile.Create(companyId, "Seller", "Seller", [
            PermissionCodes.SalesAccess,
            PermissionCodes.SalesCreate,
            PermissionCodes.SalesUpdate,
            PermissionCodes.SalesPay
        ]);
        var user = User.Create(
            Username.Create("seller"),
            eiti.Domain.Customers.Email.Create("seller@example.com"),
            PasswordHash.Create("hashed"),
            companyId,
            profile);

        var permissions = AuthenticationMapper.MapPermissions(user);

        permissions.Should().Contain(PermissionCodes.SalesAccess);
        permissions.Should().NotContain(PermissionCodes.CashAccess);
    }

    [Fact]
    public void MapPermissions_ShouldReadPermissions_FromAssignedProfile()
    {
        var companyId = CompanyId.New();
        var profile = AccessProfile.Create(companyId, "Combo", "Combo", [
            PermissionCodes.SalesAccess,
            PermissionCodes.CashAccess
        ]);
        var user = User.Create(
            Username.Create("multirole"),
            eiti.Domain.Customers.Email.Create("multi@example.com"),
            PasswordHash.Create("hashed"),
            companyId,
            profile);

        var permissions = AuthenticationMapper.MapPermissions(user);

        permissions.Should().Contain(PermissionCodes.SalesAccess);
        permissions.Should().Contain(PermissionCodes.CashAccess);
    }
}
