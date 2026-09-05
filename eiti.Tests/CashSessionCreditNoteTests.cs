using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using FluentAssertions;
using Xunit;

namespace eiti.Tests;

public class CashSessionCreditNoteTests
{
    private static CashSession OpenSession() =>
        CashSession.Open(
            CompanyId.New(),
            BranchId.New(),
            CashDrawerId.New(),
            UserId.New(),
            100_000m,
            null);

    // LA invariante del feature: una NC se ve en caja pero no mueve el efectivo esperado.
    [Fact]
    public void NotaDeCredito_NoAlteraElArqueo()
    {
        var session = OpenSession();
        var expectedBefore = session.ExpectedClosingAmount;

        session.RegisterCustomerCreditNote(50_000m, Guid.NewGuid(), "NCC-001", UserId.New());

        session.ExpectedClosingAmount.Should().Be(expectedBefore);
    }

    [Fact]
    public void NotaDeCredito_QuedaVisibleEnLosMovimientos()
    {
        var session = OpenSession();
        var noteId = Guid.NewGuid();

        session.RegisterCustomerCreditNote(50_000m, noteId, "NCC-001", UserId.New());

        var movement = session.Movements.Single(m => m.Type == CashMovementType.CustomerCreditNote);
        movement.Direction.Should().Be(CashMovementDirection.None);
        movement.Amount.Should().Be(50_000m);
        movement.ReferenceType.Should().Be(CashReferenceTypes.CreditNote);
        movement.ReferenceId.Should().Be(noteId);
        movement.Description.Should().Contain("NCC-001");
    }

    [Fact]
    public void AnulacionDeNotaDeCredito_TampocoAlteraElArqueo()
    {
        var session = OpenSession();
        var expectedBefore = session.ExpectedClosingAmount;

        session.RegisterCustomerCreditNoteCancellation(50_000m, Guid.NewGuid(), "NCC-001", UserId.New());

        session.ExpectedClosingAmount.Should().Be(expectedBefore);
        session.Movements.Should().Contain(m => m.Type == CashMovementType.CustomerCreditNoteCancellation);
    }

    [Fact]
    public void NotaDeCreditoDeProveedor_MismoTratamiento()
    {
        var session = OpenSession();
        var expectedBefore = session.ExpectedClosingAmount;

        session.RegisterSupplierCreditNote(30_000m, Guid.NewGuid(), "NCP-001", UserId.New());
        session.RegisterSupplierCreditNoteCancellation(30_000m, Guid.NewGuid(), "NCP-001", UserId.New());

        session.ExpectedClosingAmount.Should().Be(expectedBefore);
        session.Movements.Should().Contain(m => m.Type == CashMovementType.SupplierCreditNote);
        session.Movements.Should().Contain(m => m.Type == CashMovementType.SupplierCreditNoteCancellation);
    }
}
