namespace Gateway.Api.CertificationTests;

/// <summary>
/// One test per row of the Order Harmony sandbox certification checklist
/// (doc 04 §5) — production keys aren't issued until all 14 pass
/// (ARCHITECTURE.md §9). Each is skipped with the reason it can't run yet:
/// these need real Order Harmony sandbox credentials and a test store wired
/// to Eddie's Pilot till (ARCHITECTURE.md §11, Phase 3), neither of which
/// exist in this scaffold. Filling these in — replacing the Skip with a real
/// WebApplicationFactory-driven test — is literally the Definition of Done
/// for Phase 3.
/// </summary>
public class OrderHarmonyCertificationChecklistTests
{
    private const string NeedsSandbox = "Needs Order Harmony sandbox credentials and a live test store (ARCHITECTURE.md §11, Phase 3).";

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_01_Happy_path_delivery_order() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_02_Pickup_order_no_address_required() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_03_Scheduled_order_held_and_fired_at_scheduled_for() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_04_Duplicate_injection_same_idempotency_key_returns_same_response() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_05_Unknown_plu_returns_404_retryable_false() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_06_Modifier_min_max_violation_returns_422() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_07_Store_closed_returns_409() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_08_Till_offline_returns_503_retryable_true_succeeds_on_retry() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_09_Full_status_round_trip_accepted_preparing_ready_completed() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_10_Cancellation_webhook_with_valid_reason() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_11_Menu_pull_returns_stable_ids_for_categories_products_modifiers() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_12_86_an_item_propagates_within_30_seconds() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_13_Bad_webhook_signature_returns_401_and_we_retry_after_resigning() { }

    [Fact(Skip = NeedsSandbox)]
    public void Scenario_14_Multi_brand_site_prints_brand_name_on_ticket() { }
}
