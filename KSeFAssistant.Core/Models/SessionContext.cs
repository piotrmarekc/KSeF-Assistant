namespace KSeFAssistant.Core.Models;

/// <summary>
/// Aktywna sesja KSeF — przechowywana wyłącznie w pamięci (nie persystowana).
/// </summary>
public sealed class SessionContext
{
    public required string SessionToken { get; init; }
    public required string Nip { get; init; }
    public required KSeFEnvironment Environment { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public string? ReferenceNumber { get; init; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsExpiringSoon => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
}
