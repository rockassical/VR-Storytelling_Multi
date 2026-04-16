using UnityEngine;

// Phase 3: Overhangs are pre-existing in the scene (gap manually set up before play).
// Artemis exists in the scene — not spawned at runtime.
// Players swing Artemis through the overhang zone. Phase advances when all cuts are done.
public class Phase3_Trimming : NHEJPhaseHandler
{
    public override bool IsAutomatic => false;

    bool player1Cut;
    bool player2Cut;

    // ── NHEJPhaseHandler ──────────────────────────────────────────────────────

    public override void Setup()
    {
        player1Cut = false;
        player2Cut = false;
    }

    public override void StartPhase()
    {
        if (manager.IsServer)
        {
            int seed = UnityEngine.Random.Range(0, int.MaxValue);
            manager.TriggerBreakGeneration(seed);

            if (manager.BreakPoint != null)
                manager.BreakPoint.OnOverhangsResolved += OnOverhangsResolved;
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
            Debug.Log("[NHEJ Phase3] No overhangs — advancing phase.");
            manager.AdvancePhase();
        }
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        if (manager.BreakPoint != null)
            manager.BreakPoint.OnOverhangsResolved -= OnOverhangsResolved;
    }

    // ── Called by NHEJManager.ReportCutServerRpc (server-side) ───────────────

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
}
