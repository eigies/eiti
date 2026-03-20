using eiti.Application.Common.Authorization;
using eiti.Application.Features.Auth.Common;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using FluentAssertions;

namespace eiti.Tests.Unit;

public sealed class AuthenticationMapperTests
{
    [Fact]
    public void MapRolesAndPermissions_ShouldReturnRoleCodes()
    {
        var user = User.Create(
            Username.Create("testuser"),
            eiti.Domain.Customers.Email.Create("test@example.com"),
            PasswordHash.Create("hashed"),
            CompanyId.New(),
            [SystemRoles.Owner]);

        var (roles, _) = AuthenticationMapper.MapRolesAndPermissions(user);

        roles.Should().ContainSingle().Which.Should().Be(SystemRoles.Owner);
    }

    [Fact]
    public void MapRolesAndPermissions_ShouldReturnPermissionsSorted()
    {
        var user = User.Create(
            Username.Create("testuser"),
            eiti.Domain.Customers.Email.Create("test@example.com"),
            PasswordHash.Create("hashed"),
            CompanyId.New(),
            [SystemRoles.Owner]);

        var (_, permissions) = AuthenticationMapper.MapRolesAndPermissions(user);

        permissions.Should().NotBeEmpty();
        permissions.Should().BeInAscendingOrder();
    }

    [Fact]
    public void MapRolesAndPermissions_ShouldReturnCorrectPermissions_ForSellerRole()
    {
        var user = User.Create(
            Username.Create("seller"),
            eiti.Domain.Customers.Email.Create("seller@example.com"),
            PasswordHash.Create("hashed"),
            CompanyId.New(),
            [SystemRoles.Seller]);

        var (roles, permissions) = AuthenticationMapper.MapRolesAndPermissions(user);

        roles.Should().ContainSingle().Which.Should().Be(SystemRoles.Seller);
        permissions.Should().Contain(PermissionCodes.SalesAccess);
        permissions.Should().NotContain(PermissionCodes.CashAccess);
    }

    [Fact]
    public void MapRolesAndPermissions_ShouldMergePermissions_ForMultipleRoles()
    {
        var user = User.Create(
            Username.Create("multirole"),
            eiti.Domain.Customers.Email.Create("multi@example.com"),
            PasswordHash.Create("hashed"),
            CompanyId.New(),
            [SystemRoles.Seller, SystemRoles.Cashier]);

        var (roles, permissions) = AuthenticationMapper.MapRolesAndPermissions(user);

        roles.Should().HaveCount(2);
        permissions.Should().Contain(PermissionCodes.SalesAccess);
        permissions.Should().Contain(PermissionCodes.CashAccess);
    }
}
