using eiti.Domain.Common;
using eiti.Domain.Customers;
using FluentAssertions;
using Xunit;

namespace eiti.Tests;

public class CustomerCreditNoteTests
{
    private static CustomerCreditNote Sample(decimal amount = 50_000m, string reason = "Bonificación acordada") =>
        CustomerCreditNote.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "NCC-001",
            amount, reason, new DateTime(2026, 9, 5), null, Guid.NewGuid());

    [Fact]
    public void Create_NaceActiva_YRedondeaElImporte()
    {
        var note = CustomerCreditNote.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "NCC-001",
            50_000.456m, "Bonificación", new DateTime(2026, 9, 5), null, Guid.NewGuid());

        note.Status.Should().Be(CreditNoteStatus.Active);
        note.Amount.Should().Be(50_000.46m);
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

    // El motivo es la unica trazabilidad de un ajuste sin documento de origen.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RechazaMotivoVacio(string reason)
    {
        var act = () => Sample(reason: reason);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RechazaMotivoDemasiadoLargo()
    {
        var act = () => Sample(reason: new string('x', 251));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_MarcaCanceladaYGuardaQuienYCuando()
    {
        var note = Sample();
        var userId = Guid.NewGuid();

        note.Cancel(userId);

        note.Status.Should().Be(CreditNoteStatus.Cancelled);
        note.CancelledByUserId.Should().Be(userId);
        note.CancelledAt.Should().NotBeNull();
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
