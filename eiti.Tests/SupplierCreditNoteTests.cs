using eiti.Domain.Common;
using eiti.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eiti.Tests;

public class SupplierCreditNoteTests
{
    private static SupplierCreditNote Sample(decimal amount = 50_000m, string reason = "Bonificación del proveedor") =>
        SupplierCreditNote.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "NCP-001",
            amount, reason, new DateTime(2026, 9, 5), null, Guid.NewGuid());

    [Fact]
    public void Create_NaceActiva()
    {
        var note = Sample();

        note.Status.Should().Be(CreditNoteStatus.Active);
        note.Amount.Should().Be(50_000m);
        note.CancelledAt.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RechazaImporteNoPositivo(decimal amount)
    {
        var act = () => Sample(amount: amount);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RechazaMotivoVacio()
    {
        var act = () => Sample(reason: "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_DosVeces_Lanza()
    {
        var note = Sample();
        note.Cancel(Guid.NewGuid());

        var act = () => note.Cancel(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }
}
