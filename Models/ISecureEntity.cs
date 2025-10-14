namespace SubashaVentures.Models;

/// <summary>
/// Interface for entities that require audit trail and security
/// </summary>
public interface ISecureEntity
{
    string Id { get; }
    DateTime CreatedAt { get; }
    string CreatedBy { get; }
    DateTime? UpdatedAt { get; }
    string? UpdatedBy { get; }
    bool IsDeleted { get; }
    DateTime? DeletedAt { get; }
    string? DeletedBy { get; }
}
