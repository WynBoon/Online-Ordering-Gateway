namespace Gateway.Adapters.Pilot.Tests;

public class PilotIdempotencyTests
{
    [Fact]
    public void Same_order_ref_always_derives_the_same_order_id()
    {
        var first = PilotIdempotency.DeriveOrderId("OH-12345");
        var second = PilotIdempotency.DeriveOrderId("OH-12345");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Derived_order_id_fits_pilot_int32()
    {
        for (var i = 0; i < 100; i++)
        {
            var id = PilotIdempotency.DeriveOrderId($"OH-{i}-overflow-check");
            Assert.InRange(id, 0, int.MaxValue);
        }
    }
}

public class PilotStatusCodeMappingTests
{
    [Fact]
    public void Maps_the_one_confirmed_code_to_accepted()
    {
        var mapped = PilotStatusCodeMapping.TryMap(2, out var status);

        Assert.True(mapped);
        Assert.Equal(Domain.Enums.OrderStatus.Accepted, status);
    }

    [Fact]
    public void Rejects_unconfirmed_codes_rather_than_guessing()
    {
        var mapped = PilotStatusCodeMapping.TryMap(999, out _);

        Assert.False(mapped);
    }
}
