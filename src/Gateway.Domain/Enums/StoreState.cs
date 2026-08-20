namespace Gateway.Domain.Enums;

/// <summary>
/// Every use case gates on this before processing anything for a store
/// (ARCHITECTURE.md §7, "Store lifecycle"). A document doesn't get to flow
/// through the gateway just because it arrived — the store has to allow it.
/// </summary>
public enum StoreState
{
    /// <summary>Being onboarded. No inbound traffic is processed even if it arrives.</summary>
    Draft,

    /// <summary>Normal operation, all use cases process.</summary>
    Active,

    /// <summary>
    /// Order injection is rejected outright. Never auto-resumes — returning to
    /// Active is always an explicit admin action, to avoid flapping.
    /// </summary>
    Paused,

    /// <summary>Terminal. Not reversible — reactivating means re-onboarding as if new.</summary>
    Deactivated
}
