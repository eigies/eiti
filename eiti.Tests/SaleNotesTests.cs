using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Quotes.Commands.ConvertQuoteToSale;
using eiti.Application.Features.Sales.Commands.CreateCcSale;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Application.Features.Sales.Commands.UpdateSaleNotes;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using eiti.Domain.Sales;
using FluentAssertions;
using MediatR;
using Moq;

namespace eiti.Tests;

public sealed class SaleNotesTests
{
    private static readonly string TooLong = new('x', Sale.NotesMaxLength + 1);

    private static Sale BuildSale()
    {
        var companyId = CompanyId.New();
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Moura", "Bateria", null, 99000m, 70000m, null);
        return Sale.Create(companyId, BranchId.New(), null, false, SaleStatus.OnHold, [SaleDetail.Create(product.Id, 1, product.Price)]);
    }

    [Fact]
    public void SetNotes_TrimsTheText()
    {
        var sale = BuildSale();

        sale.SetNotes("  Presup. 1234 - Patente AB123CD  ");

        sale.Notes.Should().Be("Presup. 1234 - Patente AB123CD");
    }

    [Fact]
    public void SetNotes_BlankMeansNoNotes()
    {
        var sale = BuildSale();
        sale.SetNotes("Patente AB123CD");

        sale.SetNotes("   ");

        sale.Notes.Should().BeNull();
    }

    [Fact]
    public void SetNotes_RejectsMoreThanTheMaximum()
    {
        var sale = BuildSale();

        var act = () => sale.SetNotes(TooLong);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateSaleValidator_RejectsNotesOverTheMaximum()
    {
        var command = new CreateSaleCommand(Guid.NewGuid(), null, 1, false, null, [], [], [], Notes: TooLong);

        var result = new CreateSaleValidator().Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateSaleCommand.Notes));
    }

    [Fact]
    public void CreateCcSaleValidator_RejectsNotesOverTheMaximum()
    {
        var command = new CreateCcSaleCommand(Guid.NewGuid(), Guid.NewGuid(), [], Notes: TooLong);

        var result = new CreateCcSaleValidator().Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateCcSaleCommand.Notes));
    }

    [Fact]
    public async Task ConvertQuoteToSale_ForwardsTheNotesToTheSale()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var quote = Quote.Create(
            companyId, branchId, CustomerId.New(), null, null,
            new[] { QuoteDetail.Create(ProductId.New(), 1, 100m) },
            0, DateTime.UtcNow.AddDays(7), Guid.NewGuid());

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUser.SetupGet(s => s.CompanyId).Returns(companyId);
        var quotes = new Mock<IQuoteRepository>();
        quotes.Setup(r => r.GetByIdAsync(quote.Id, companyId, It.IsAny<CancellationToken>())).ReturnsAsync(quote);

        CreateCcSaleCommand? sent = null;
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<CreateCcSaleCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<CreateCcSaleResponse>>, CancellationToken>((command, _) => sent = (CreateCcSaleCommand)command)
            .ReturnsAsync(Result<CreateCcSaleResponse>.Success(new CreateCcSaleResponse(
                Guid.NewGuid(), "SC-001", branchId.Value, Guid.NewGuid(), "Juan Perez",
                1, "OnHold", 0, 100m, 100m, null, null, null, true, DateTime.UtcNow,
                0, 0, [], 0, 100m, [])));

        var handler = new ConvertQuoteToSaleHandler(currentUser.Object, quotes.Object, sender.Object, new Mock<IUnitOfWork>().Object);

        await handler.Handle(
            new ConvertQuoteToSaleCommand(
                quote.Id.Value, branchId.Value, Guid.NewGuid(),
                [new CreateSaleDetailItemRequest(Guid.NewGuid(), 1, null, 0)],
                Notes: "Presupuesto PRE-0012"),
            CancellationToken.None);

        sent!.Notes.Should().Be("Presupuesto PRE-0012");
    }

    private static (UpdateSaleNotesHandler Handler, Mock<IUnitOfWork> UnitOfWork) BuildUpdateHandler(Sale? sale)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUser.SetupGet(s => s.CompanyId).Returns(sale?.CompanyId ?? CompanyId.New());
        var sales = new Mock<ISaleRepository>();
        sales.Setup(r => r.GetByIdAsync(It.IsAny<SaleId>(), It.IsAny<CancellationToken>())).ReturnsAsync(sale);
        var unitOfWork = new Mock<IUnitOfWork>();
        return (new UpdateSaleNotesHandler(currentUser.Object, sales.Object, unitOfWork.Object), unitOfWork);
    }

    [Fact]
    public async Task UpdateSaleNotes_FailsWhenTheSaleDoesNotExist()
    {
        var (handler, _) = BuildUpdateHandler(null);

        var result = await handler.Handle(new UpdateSaleNotesCommand(Guid.NewGuid(), "Patente AB123CD"), CancellationToken.None);

        result.Error.Should().Be(UpdateSaleNotesErrors.NotFound);
    }

    [Fact]
    public async Task UpdateSaleNotes_FailsOnACancelledSale()
    {
        var sale = BuildSale();
        sale.Cancel();
        var (handler, unitOfWork) = BuildUpdateHandler(sale);

        var result = await handler.Handle(new UpdateSaleNotesCommand(sale.Id.Value, "Patente AB123CD"), CancellationToken.None);

        result.Error.Should().Be(UpdateSaleNotesErrors.Cancelled);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // La nota no es dato fiscal: corregirla en una venta cobrada (y facturada) no toca el comprobante.
    [Fact]
    public async Task UpdateSaleNotes_UpdatesAPaidSale()
    {
        var sale = BuildSale();
        sale.SetNotes("Presup. 1234");
        var (handler, unitOfWork) = BuildUpdateHandler(sale);

        var result = await handler.Handle(new UpdateSaleNotesCommand(sale.Id.Value, " Presup. 1234 - Patente AB123CD "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sale.Notes.Should().Be("Presup. 1234 - Patente AB123CD");
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void UpdateSaleNotesValidator_RejectsNotesOverTheMaximum()
    {
        var result = new UpdateSaleNotesValidator().Validate(new UpdateSaleNotesCommand(Guid.NewGuid(), TooLong));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpdateSaleNotesCommand.Notes));
    }
}
