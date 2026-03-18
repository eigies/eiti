using eiti.Domain.Primitives;
using eiti.Domain.Users;

namespace eiti.Domain.Cash;

public sealed class CashMovement : Entity<CashMovementId>
{
    public CashSessionId CashSessionId { get; private set; }
    public CashMovementType Type { get; private set; }
    public CashMovementDirection Direction { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string Description { get; private set; }
    public UserId CreatedByUserId { get; private set; }
    public Guid? TransferCounterpartSessionId { get; private set; }

    private CashMovement()
    {
    }

    private CashMovement(
        CashMovementId id,
        CashSessionId cashSessionId,
        CashMovementType type,
        CashMovementDirection direction,
        decimal amount,
        string? referenceType,
        Guid? referenceId,
        string description,
        UserId createdByUserId,
        Guid? transferCounterpartSessionId = null)
        : base(id)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Cash movement amount must be greater than zero.", nameof(amount));
        }

        CashSessionId = cashSessionId;
        Type = type;
        Direction = direction;
        Amount = amount;
        OccurredAt = DateTime.UtcNow;
        ReferenceType = NormalizeOptional(referenceType, 50, "Reference type");
        ReferenceId = referenceId;
        Description = NormalizeRequired(description, 255, "Description");
        CreatedByUserId = createdByUserId;
        TransferCounterpartSessionId = transferCounterpartSessionId;
    }

    public static CashMovement Create(
        CashSessionId cashSessionId,
        CashMovementType type,
        CashMovementDirection direction,
        decimal amount,
        string? referenceType,
        Guid? referenceId,
        string description,
        UserId createdByUserId,
        Guid? transferCounterpartSessionId = null)
    {
        return new CashMovement(
            CashMovementId.New(),
            cashSessionId,
            type,
            direction,
            amount,
            referenceType,
            referenceId,
            description,
            createdByUserId,
            transferCounterpartSessionId);
    }

    private static string NormalizeRequired(string value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{field} cannot be empty.", nameof(value));
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }
}
