namespace Gateway.Domain.Enums;

/// <summary>Matches Order Harmony's cancel reason vocabulary exactly (doc 02).</summary>
public enum CancelReason
{
    OutOfStock,
    StoreClosed,
    PosFailure,
    MerchantRejected,
    CustomerRequest,
    Other
}
