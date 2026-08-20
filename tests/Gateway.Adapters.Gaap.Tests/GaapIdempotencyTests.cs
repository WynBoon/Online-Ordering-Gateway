namespace Gateway.Adapters.Gaap.Tests;

public class GaapIdempotencyTests
{
    [Fact]
    public void Same_order_ref_always_derives_the_same_transaction_id()
    {
        var first = GaapIdempotency.DeriveTransactionId("OH-12345");
        var second = GaapIdempotency.DeriveTransactionId("OH-12345");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_order_refs_derive_different_transaction_ids()
    {
        var a = GaapIdempotency.DeriveTransactionId("OH-12345");
        var b = GaapIdempotency.DeriveTransactionId("OH-67890");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Derived_id_is_a_valid_guid()
    {
        var id = GaapIdempotency.DeriveTransactionId("OH-12345");

        Assert.True(Guid.TryParse(id, out _));
    }
}
