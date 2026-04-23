namespace eiti.Domain.Users;

public sealed class PasswordResetToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }

    private PasswordResetToken()
    {
    }

    private PasswordResetToken(Guid id, Guid userId, string codeHash, DateTime expiresAt)
    {
        Id = id;
        UserId = userId;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
    }

    public static PasswordResetToken Create(Guid userId, string codeHash, DateTime expiresAt)
        => new(Guid.NewGuid(), userId, codeHash, expiresAt);

    public bool IsActive => UsedAt is null && ExpiresAt > DateTime.UtcNow;

    public void MarkAsUsed()
    {
        UsedAt = DateTime.UtcNow;
    }
}
