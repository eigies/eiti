using eiti.Domain.Banks;
using eiti.Domain.Companies;
using FluentAssertions;

namespace eiti.Tests;

public sealed class BankUsageCapabilitiesTests
{
    [Fact]
    public void Create_ShouldEnableAllCapabilitiesByDefault()
    {
        var bank = Bank.Create(CompanyId.New(), "Banco Galicia");

        bank.UseForCard.Should().BeTrue();
        bank.UseForTransfer.Should().BeTrue();
        bank.UseForCheque.Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldPersistCapabilityFlags()
    {
        var bank = Bank.Create(CompanyId.New(), "Banco Galicia");

        bank.Update("Banco Galicia", active: true, useForCard: false, useForTransfer: true, useForCheque: false);

        bank.UseForCard.Should().BeFalse();
        bank.UseForTransfer.Should().BeTrue();
        bank.UseForCheque.Should().BeFalse();
    }
}
