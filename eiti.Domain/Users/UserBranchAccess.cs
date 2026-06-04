using eiti.Domain.Branches;
using eiti.Domain.Primitives;

namespace eiti.Domain.Users;

public sealed class UserBranchAccess : Entity<Guid>
{
    public UserId UserId { get; private set; } = null!;
    public BranchId BranchId { get; private set; } = null!;

    private UserBranchAccess()
    {
    }

    private UserBranchAccess(Guid id, UserId userId, BranchId branchId)
        : base(id)
    {
        UserId = userId;
        BranchId = branchId;
    }

    public static UserBranchAccess Create(UserId userId, BranchId branchId)
        => new(Guid.NewGuid(), userId, branchId);
}
