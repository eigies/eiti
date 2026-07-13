using eiti.Domain.Banks;

namespace eiti.Application.Features.Banks.Common;

public static class BankUsageRules
{
    public static bool Supports(Bank? bank, BankUsage usage)
    {
        if (bank is null || !bank.Active)
        {
            return false;
        }

        return usage switch
        {
            BankUsage.Card => bank.UseForCard,
            BankUsage.Transfer => bank.UseForTransfer,
            BankUsage.Cheque => bank.UseForCheque,
            BankUsage.All => true,
            _ => false
        };
    }
}
