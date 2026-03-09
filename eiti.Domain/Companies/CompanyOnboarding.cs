using eiti.Domain.Primitives;

namespace eiti.Domain.Companies;

public sealed class CompanyOnboarding : AggregateRoot<CompanyId>
{
    public bool HasCreatedBranch { get; private set; }
    public bool HasCreatedCashDrawer { get; private set; }
    public bool HasCompletedInitialCashOpen { get; private set; }
    public bool HasCreatedProduct { get; private set; }
    public bool HasLoadedInitialStock { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CompanyOnboarding()
    {
    }

    private CompanyOnboarding(
        CompanyId companyId,
        bool hasCreatedBranch,
        bool hasCreatedCashDrawer,
        bool hasCompletedInitialCashOpen,
        bool hasCreatedProduct,
        bool hasLoadedInitialStock,
        DateTime? completedAt,
        DateTime updatedAt)
        : base(companyId)
    {
        HasCreatedBranch = hasCreatedBranch;
        HasCreatedCashDrawer = hasCreatedCashDrawer;
        HasCompletedInitialCashOpen = hasCompletedInitialCashOpen;
        HasCreatedProduct = hasCreatedProduct;
        HasLoadedInitialStock = hasLoadedInitialStock;
        CompletedAt = completedAt;
        UpdatedAt = updatedAt;
    }

    public static CompanyOnboarding CreateIncomplete(CompanyId companyId)
    {
        return new CompanyOnboarding(
            companyId,
            false,
            false,
            false,
            false,
            false,
            null,
            DateTime.UtcNow);
    }

    public static CompanyOnboarding CreateCompleted(CompanyId companyId)
    {
        var now = DateTime.UtcNow;

        return new CompanyOnboarding(
            companyId,
            true,
            true,
            true,
            true,
            true,
            now,
            now);
    }

    public void MarkBranchCreated()
    {
        HasCreatedBranch = true;
        Touch();
    }

    public void MarkCashDrawerCreated()
    {
        HasCreatedCashDrawer = true;
        Touch();
    }

    public void MarkInitialCashOpenCompleted()
    {
        HasCompletedInitialCashOpen = true;
        Touch();
    }

    public void MarkProductCreated()
    {
        HasCreatedProduct = true;
        Touch();
    }

    public void MarkInitialStockLoaded()
    {
        HasLoadedInitialStock = true;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTime.UtcNow;

        if (HasCreatedBranch
            && HasCreatedCashDrawer
            && HasCompletedInitialCashOpen
            && HasCreatedProduct
            && HasLoadedInitialStock)
        {
            CompletedAt ??= UpdatedAt;
        }
    }
}
