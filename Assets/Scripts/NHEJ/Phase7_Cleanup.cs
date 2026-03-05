using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Phase 7: Two VCP/ATPase proteins orbit the break site.
// P1 places their VCP onto the left Ku; P2 places theirs onto the right Ku.
// Placement triggers Ku departure animation (all clients via OnProteinPlacedLocal).
// When both VCPs are placed: scaffold dismantles locally, repaired DNA reveals, then advance.
public class Phase7_Cleanup : NHEJPhaseHandler
{
    [Header("References to Earlier Phases")]
    [SerializeField] Phase1_KuBinding phase1Handler;
    [SerializeField] Phase5_Alignment phase5Handler;

    [Header("VCP/ATPase Pickup Prefab")]
    [Tooltip("NetworkObject + NHEJTool + ProteinOrbitController + NHEJProteinNPC")]
    [SerializeField] GameObject vcpPickupPrefab;

    [Header("Placement Targets (over left/right Ku positions)")]
    [SerializeField] Transform leftKuTarget;    // P1 places VCP here
    [SerializeField] Transform rightKuTarget;   // P2 places VCP here

    [Header("Repaired DNA")]
    [SerializeField] GameObject repairedDNAVisual;

    [Header("Timing")]
    [SerializeField] float kuRemovalDuration = 2f;
    [SerializeField] float postCleanupPause  = 1f;

    public override bool IsAutomatic => false;

    NetworkObject vcp1Object;
    NetworkObject vcp2Object;

    // Server-side tracking
    bool serverP1Placed;
    bool serverP2Placed;

    // Local tracking (all clients)
    bool localP1Placed;
    bool localP2Placed;

    public override void Setup()
    {
        serverP1Placed = serverP2Placed = false;
        localP1Placed  = localP2Placed  = false;
    }

    public override void StartPhase()
    {
        if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlayPhaseAdvance();

        if (!manager.IsServer || vcpPickupPrefab == null) return;

        Vector3 center = GetDNACenter();

        Vector3 p1Snap = leftKuTarget  != null ? leftKuTarget.position
                       : (phase1Handler?.LeftKu  != null ? phase1Handler.LeftKu.transform.position  : center + Vector3.left  * 0.3f);
        Vector3 p2Snap = rightKuTarget != null ? rightKuTarget.position
                       : (phase1Handler?.RightKu != null ? phase1Handler.RightKu.transform.position : center + Vector3.right * 0.3f);

        vcp1Object = SpawnProtein(center, 1, p1Snap);
        vcp2Object = SpawnProtein(center, 2, p2Snap);
    }

    NetworkObject SpawnProtein(Vector3 spawnPos, int role, Vector3 snapPos)
    {
        var go = Instantiate(vcpPickupPrefab, spawnPos, Quaternion.identity);
        go.GetComponent<ProteinOrbitController>()?.Configure(role, snapPos);
        var no = go.GetComponent<NetworkObject>();
        no?.Spawn();
        return no;
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        if (!manager.IsServer) return;
        if (vcp1Object != null && vcp1Object.IsSpawned) vcp1Object.Despawn();
        if (vcp2Object != null && vcp2Object.IsSpawned) vcp2Object.Despawn();
    }

    // ── Server-side placement handler ───────────────────────────────────────────

    public override void OnProteinPlaced(int playerRole)
    {
        if (playerRole == 1) serverP1Placed = true;
        else if (playerRole == 2) serverP2Placed = true;

        if (serverP1Placed && serverP2Placed)
            StartCoroutine(FinishCleanup());
    }

    // ── All-client visual handler ────────────────────────────────────────────────

    public override void OnProteinPlacedLocal(int playerRole)
    {
        if (playerRole == 1)
        {
            localP1Placed = true;
            // Animate left Ku departure
            if (phase1Handler?.LeftKu != null)
            {
                var npc = phase1Handler.LeftKu.GetComponent<NHEJProteinNPC>();
                if (npc != null) StartCoroutine(npc.Depart(Vector3.left + Vector3.up, 4f, kuRemovalDuration));
            }
        }
        else if (playerRole == 2)
        {
            localP2Placed = true;
            // Animate right Ku departure
            if (phase1Handler?.RightKu != null)
            {
                var npc = phase1Handler.RightKu.GetComponent<NHEJProteinNPC>();
                if (npc != null) StartCoroutine(npc.Depart(Vector3.right + Vector3.up, 4f, kuRemovalDuration));
            }
        }

        if (localP1Placed && localP2Placed)
        {
            // Dismantle scaffold and reveal repaired DNA after Ku removal.
            StartCoroutine(LocalCleanupVisuals());
        }
    }

    // ── Coroutines ───────────────────────────────────────────────────────────────

    // Server-only: waits for animations then despawns Ku objects and advances.
    IEnumerator FinishCleanup()
    {
        yield return new WaitForSeconds(kuRemovalDuration);

        // Despawn Ku NetworkObjects (departure animation done by now).
        var leftNO  = phase1Handler?.LeftKuNetworkObject;
        var rightNO = phase1Handler?.RightKuNetworkObject;
        if (leftNO  != null && leftNO.IsSpawned)  leftNO.Despawn();
        if (rightNO != null && rightNO.IsSpawned) rightNO.Despawn();

        yield return new WaitForSeconds(postCleanupPause);
        manager.AdvancePhase();
    }

    // All clients: scaffold dismantle + repaired DNA reveal.
    IEnumerator LocalCleanupVisuals()
    {
        yield return new WaitForSeconds(kuRemovalDuration * 0.5f);

        // Dismantle XRCC4/XLF scaffold
        if (phase5Handler?.ScaffoldSegments != null)
        {
            var segments = phase5Handler.ScaffoldSegments;
            float segDelay = 0.4f / Mathf.Max(1, segments.Count);
            foreach (var seg in segments)
            {
                if (seg == null) continue;
                var npc = seg.GetComponent<NHEJProteinNPC>();
                if (npc != null)
                {
                    Vector3 center = GetDNACenter();
                    Vector3 dir = (seg.transform.position - center).normalized;
                    StartCoroutine(npc.Depart(dir + Vector3.up, 3f, 1.5f));
                }
                yield return new WaitForSeconds(segDelay);
            }
        }

        yield return new WaitForSeconds(kuRemovalDuration * 0.5f);

        if (repairedDNAVisual != null)
            repairedDNAVisual.SetActive(true);
    }

    Vector3 GetDNACenter()
    {
        if (manager.LeftDNAEnd != null && manager.RightDNAEnd != null)
            return (manager.LeftDNAEnd.position + manager.RightDNAEnd.position) * 0.5f;
        return manager.LeftDNAEnd != null ? manager.LeftDNAEnd.position : Vector3.zero;
    }
}
