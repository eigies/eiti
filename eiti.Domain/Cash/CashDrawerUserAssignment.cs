using eiti.Domain.Primitives;
using eiti.Domain.Users;

namespace eiti.Domain.Cash;

public sealed class CashDrawerUserAssignment : Entity<Guid>
{
    public CashDrawerId CashDrawerId { get; private set; } = null!;
    public UserId UserId { get; private set; } = null!;
    public DateTime AssignedAt { get; private set; }

    private CashDrawerUserAssignment()
    {
    }

    private CashDrawerUserAssignment(
        Guid id,
        CashDrawerId cashDrawerId,
        UserId userId,
        DateTime assignedAt)
        : base(id)
    {
        CashDrawerId = cashDrawerId;
        UserId = userId;
        AssignedAt = assignedAt;
    }

    public static CashDrawerUserAssignment Create(CashDrawerId cashDrawerId, UserId userId)
    {
        return new CashDrawerUserAssignment(Guid.NewGuid(), cashDrawerId, userId, DateTime.UtcNow);
    }
}
