using Unity.Netcode;
using UnityEngine;

// Phase 3: NHEJBreakPoint picks a random cut site in the DNA helix, blows up the centre,
// and leaves glowing overhangs. Players grab Artemis and swing it through the overhang zone
// (ArtemisBlade child trigger + runtime-generated OverhangZone). The cut fires wherever the
// blade enters. Overhang segments are hidden and seam indicators removed by NHEJBreakPoint.
// Phase advances when all required overhangs are cut.
public class Phase3_Trimming : NHEJPhaseHandler
{
    [Header("Tool Prefab")]
    [SerializeField] GameObject artemisToolPrefab;

    public override bool IsAutomatic => false;

    bool player1Cut;
    bool player2Cut;
    NetworkObject artemisObject;

    // ── NHEJPhaseHandler ──────────────────────────────────────────────────────

    public override void Setup()
    {
        player1Cut = false;
        player2Cut = false;
    }

    public override void StartPhase()
    {
        DSBScenario scenario   = manager.Scenario;
        bool p1NeedsCut = scenario == DSBScenario.LeftOverhangOnly  || scenario == DSBScenario.BothOverhangs;
        bool p2NeedsCut = scenario == DSBScenario.RightOverhangOnly || scenario == DSBScenario.BothOverhangs;

        Debug.Log($"[NHEJ] Phase3 scenario={scenario} p1Cut={p1NeedsCut} p2Cut={p2NeedsCut}");

        if (manager.IsServer)
        {
            // Generate the random break on all clients (same seed = deterministic result everywhere).
            int seed = UnityEngine.Random.Range(0, int.MaxValue);
            manager.TriggerBreakGeneration(seed);

            // Listen for overhang result. If the explosion left no overhangs (no red walls),
            // skip Artemis entirely and advance straight to the next phase.
            if (manager.BreakPoint != null)
                manager.BreakPoint.OnOverhangsResolved += OnOverhangsResolved;

            if ((p1NeedsCut || p2NeedsCut) && artemisToolPrefab != null)
            {
                var go = Instantiate(artemisToolPrefab, GetArtemisSpawnPos(), Quaternion.identity);
                artemisObject = go.GetComponent<NetworkObject>();
                artemisObject.Spawn();
            }

            // Auto-complete players who don't need a cut in this scenario.
            if (!p1NeedsCut) manager.ServerMarkPlayerComplete(1);
            if (!p2NeedsCut) manager.ServerMarkPlayerComplete(2);
        }

        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayPhaseAdvance();
    }

    void OnOverhangsResolved(bool hasOverhangs)
    {
        if (manager.BreakPoint != null)
            manager.BreakPoint.OnOverhangsResolved -= OnOverhangsResolved;

        if (!hasOverhangs)
        {
            Debug.Log("[NHEJ Phase3] No overhangs detected — skipping Artemis, advancing phase.");
            manager.AdvancePhase();
        }
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        if (manager.BreakPoint != null)
            manager.BreakPoint.OnOverhangsResolved -= OnOverhangsResolved;

        if (manager.IsServer && artemisObject != null && artemisObject.IsSpawned)
            artemisObject.Despawn();
    }

    // ── Called by NHEJManager.ReportCutServerRpc (server-side) ───────────────

    /// <summary>
    /// Server-only. Called after a validated cut for the given player role.
    /// Marks that player complete; phase advances when all required cuts are done.
    /// </summary>
    public void OnCutMade(int playerRole)
    {
        if (!manager.IsServer) return;

        if (playerRole == 1 && !player1Cut)
        {
            player1Cut = true;
            manager.ServerMarkPlayerComplete(1);
            Debug.Log("[NHEJ Phase3] Player 1 cut confirmed.");
        }
        else if (playerRole == 2 && !player2Cut)
        {
            player2Cut = true;
            manager.ServerMarkPlayerComplete(2);
            Debug.Log("[NHEJ Phase3] Player 2 cut confirmed.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    Vector3 GetArtemisSpawnPos()
    {
        // Spawn Artemis near the DNA centre with a small offset so it's reachable in VR.
        Vector3 centre = manager.LeftDNAEnd != null && manager.RightDNAEnd != null
            ? (manager.LeftDNAEnd.position + manager.RightDNAEnd.position) * 0.5f
            : (manager.LeftDNAEnd?.position ?? Vector3.zero);
        return centre + Vector3.up * 0.3f;
    }

    /* DISABLED — TrimPoint mechanic preserved for reversion:

    [Header("Trim Points (old snap-based mechanic — disabled)")]
    [SerializeField] TrimPoint[] player1TrimPoints;
    [SerializeField] TrimPoint[] player2TrimPoints;

    public bool ValidateTrim(int pointIndex, int playerRole) { ... }
    public void ServerMarkTrimmed(int pointIndex, int role) { ... }
    public void OnTrimConfirmed(int pointIndex, ulong clientId) { ... }
    */
}
