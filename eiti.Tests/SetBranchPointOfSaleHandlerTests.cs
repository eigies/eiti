using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Branches.Commands.SetBranchPointOfSale;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

/// <summary>
/// Cada sucursal factura en su punto de venta de ARCA (1:1). El número se habilita primero en el
/// servicio de facturación: si no lo acepta, la sucursal no queda con un punto de venta inservible.
/// </summary>
public sealed class SetBranchPointOfSaleHandlerTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _branch;
    private readonly Mock<IFiscalPointOfSaleRepository> _pointsOfSale = new();
    private readonly Mock<IFiscalizationService> _fiscal = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public SetBranchPointOfSaleHandlerTests()
    {
        _branch = Branch.Create(_companyId, "Sucursal Centro", "SC", "San Martin 123");
        _fiscal.SetupGet(x => x.IsEnabled).Returns(true);
        _fiscal.Setup(x => x.RegisterPointOfSaleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalOperationResult(true));
    }

    [Fact]
    public async Task Assigns_the_point_of_sale_after_enabling_it_in_the_fiscal_service()
    {
        var result = await Set(3);

        result.IsSuccess.Should().BeTrue();
        result.Value.FiscalPointOfSaleNumber.Should().Be(3);
        _branch.FiscalPointOfSale!.Number.Should().Be(3);
        _fiscal.Verify(x => x.RegisterPointOfSaleAsync(_companyId.Value, 3, It.IsAny<CancellationToken>()), Times.Once);
        _pointsOfSale.Verify(x => x.AddAsync(It.Is<FiscalPointOfSale>(p => p.Number == 3), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task If_the_fiscal_service_refuses_it_nothing_is_saved()
    {
        _fiscal.Setup(x => x.RegisterPointOfSaleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalOperationResult(false, "El servicio de facturación todavía no tiene un perfil fiscal activo para esta empresa."));

        var result = await Set(3);

        result.Error.Code.Should().Be("Branches.PointOfSale.NotRegistered");
        result.Error.Description.Should().Contain("no tiene un perfil fiscal activo");
        _branch.FiscalPointOfSale.Should().BeNull();
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_number_used_by_another_branch_is_refused()
    {
        _pointsOfSale.Setup(x => x.NumberInUseAsync(_companyId, 3, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Set(3);

        result.Error.Code.Should().Be("Branches.PointOfSale.InUse");
        _fiscal.Verify(x => x.RegisterPointOfSaleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100000)]
    public async Task An_out_of_range_number_is_refused(int number)
    {
        var result = await Set(number);

        result.Error.Code.Should().Be("Branches.PointOfSale.OutOfRange");
    }

    [Fact]
    public async Task Changing_the_number_keeps_the_same_row()
    {
        var current = FiscalPointOfSale.Create(_companyId, 3);
        _branch.AssignFiscalPointOfSale(current);

        var result = await Set(4);

        result.Value.FiscalPointOfSaleNumber.Should().Be(4);
        _branch.FiscalPointOfSale.Should().BeSameAs(current);
        _pointsOfSale.Verify(x => x.NumberInUseAsync(_companyId, 4, current.Id, It.IsAny<CancellationToken>()), Times.Once);
        _pointsOfSale.Verify(x => x.AddAsync(It.IsAny<FiscalPointOfSale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_same_number_does_not_call_the_fiscal_service_again()
    {
        _branch.AssignFiscalPointOfSale(FiscalPointOfSale.Create(_companyId, 3));

        var result = await Set(3);

        result.IsSuccess.Should().BeTrue();
        _fiscal.Verify(x => x.RegisterPointOfSaleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Null_removes_the_point_of_sale_and_frees_the_number()
    {
        var current = FiscalPointOfSale.Create(_companyId, 3);
        _branch.AssignFiscalPointOfSale(current);

        var result = await Set(null);

        result.Value.FiscalPointOfSaleNumber.Should().BeNull();
        _branch.FiscalPointOfSaleId.Should().BeNull();
        _pointsOfSale.Verify(x => x.Remove(current), Times.Once);
    }

    [Fact]
    public async Task Without_fiscalization_configured_it_is_only_saved_in_eiti()
    {
        _fiscal.SetupGet(x => x.IsEnabled).Returns(false);

        var result = await Set(3);

        result.IsSuccess.Should().BeTrue();
        _fiscal.Verify(x => x.RegisterPointOfSaleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Task<eiti.Application.Common.Result<eiti.Application.Features.Branches.Common.BranchResponse>> Set(int? number)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(_branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_branch);

        var handler = new SetBranchPointOfSaleHandler(currentUser.Object, branches.Object, _pointsOfSale.Object, _fiscal.Object, _unitOfWork.Object);
        return handler.Handle(new SetBranchPointOfSaleCommand(_branch.Id.Value, number), CancellationToken.None);
    }
}
