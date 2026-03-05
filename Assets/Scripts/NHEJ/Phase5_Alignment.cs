using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Phase 5: DNA-PKcs open and drift away automatically, then two XRCC4/XLF scaffold proteins
// orbit the break site. P1 places their piece at the left scaffold anchor; P2 at the right.
// When both are placed (all clients): the full scaffold builds and the DNA ends pull together.
// Phase advances after the alignment animation completes.
public class Phase5_Alignment : NHEJPhaseHandler
{
    [Header("References to Earlier Phase Objects")]
    [SerializeField] Phase2_DNAPKcs phase2Handler;

    [Header("XRCC4/XLF Pickup Prefab")]
    [Tooltip("NetworkObject + NHEJTool + ProteinOrbitController + NHEJProteinNPC")]
    [SerializeField] GameObject xrcc4PickupPrefab;

    [Header("XRCC4/XLF Visual Scaffold (local-only, spawned after both proteins placed)")]
    [SerializeField] GameObject xrcc4XlfSegmentPrefab;
    [SerializeField] int scaffoldSegmentCount = 4;
    [SerializeField] Transform scaffoldStartPoint;
    [SerializeField] Transform scaffoldEndPoint;

    [Header("DNA End Alignment")]
    [SerializeField] Transform leftEndAlignTarget;
    [SerializeField] Transform rightEndAlignTarget;
    [SerializeField] float alignDuration = 2f;

    [Header("Timing")]
    [SerializeField] float pkcsDepartDuration = 1.5f;
    [SerializeField] float postAlignPause = 1f;
    [Tooltip("Extra buffer (seconds) so the server waits for scaffold+align before advancing.")]
    [SerializeField] float advanceBuffer = 1.5f;

    public override bool IsAutomatic => false;
    public List<GameObject> ScaffoldSegments => scaffoldSegments;

    readonly List<GameObject> scaffoldSegments = new();

    NetworkObject xrcc4Object1;
    NetworkObject xrcc4Object2;

    // Server-side placement tracking
    bool serverP1Placed;
    bool serverP2Placed;

    // Local placement tracking (all clients — driven by OnProteinPlacedLocal)
    bool localP1Placed;
    bool localP2Placed;
    Coroutine localAnimCoroutine;

    Coroutine runCoroutine;

    public override void Setup()
    {
        scaffoldSegments.Clear();
        serverP1Placed = serverP2Placed = false;
        localP1Placed  = localP2Placed  = false;
        localAnimCoroutine = null;
    }

    public override void StartPhase()
    {
        if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlayPhaseAdvance();
        runCoroutine = StartCoroutine(RunPhase());
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        if (runCoroutine != null) { StopCoroutine(runCoroutine); runCoroutine = null; }
        if (localAnimCoroutine != null) { StopCoroutine(localAnimCoroutine); localAnimCoroutine = null; }

        if (manager.IsServer)
        {
            if (xrcc4Object1 != null && xrcc4Object1.IsSpawned) xrcc4Object1.Despawn();
            if (xrcc4Object2 != null && xrcc4Object2.IsSpawned) xrcc4Object2.Despawn();
        }
    }

    // ── Server-side placement handler ───────────────────────────────────────────

    public override void OnProteinPlaced(int playerRole)
    {
        if (playerRole == 1) serverP1Placed = true;
        else if (playerRole == 2) serverP2Placed = true;

        if (serverP1Placed && serverP2Placed)
        {
            // Scaffold build + alignment happen locally (OnProteinPlacedLocal).
            // Wait for them to finish, then advance.
            float segBuildTime = scaffoldSegmentCount * 0.3f;
            float totalDelay   = segBuildTime + alignDuration + postAlignPause + advanceBuffer;
            StartCoroutine(WaitThenAdvance(totalDelay));
        }
    }

    // ── All-client visual handler ────────────────────────────────────────────────

    public override void OnProteinPlacedLocal(int playerRole)
    {
        if (playerRole == 1) localP1Placed = true;
        else if (playerRole == 2) localP2Placed = true;

        if (localP1Placed && localP2Placed && localAnimCoroutine == null)
            localAnimCoroutine = StartCoroutine(BuildScaffoldAndAlign());
    }

    // ── Coroutines ───────────────────────────────────────────────────────────────

    IEnumerator RunPhase()
    {
        // Auto: DNA-PKcs drift away locally on all clients.
        if (phase2Handler != null)
        {
            var leftNPC  = phase2Handler.LeftPKcs?.GetComponent<NHEJProteinNPC>();
            var rightNPC = phase2Handler.RightPKcs?.GetComponent<NHEJProteinNPC>();
            if (leftNPC  != null) StartCoroutine(leftNPC.Depart(Vector3.up  + Vector3.left,  3f, pkcsDepartDuration));
            if (rightNPC != null) StartCoroutine(rightNPC.Depart(Vector3.up + Vector3.right, 3f, pkcsDepartDuration));
        }
        yield return new WaitForSeconds(pkcsDepartDuration);

        // Server despawns the PKcs NetworkObjects after departure animation.
        if (manager.IsServer && phase2Handler != null)
        {
            var leftNO  = phase2Handler.LeftPKcsNetworkObject;
            var rightNO = phase2Handler.RightPKcsNetworkObject;
            if (leftNO  != null && leftNO.IsSpawned)  leftNO.Despawn();
            if (rightNO != null && rightNO.IsSpawned) rightNO.Despawn();
        }

        // Server spawns interactive XRCC4/XLF pickup proteins.
        if (manager.IsServer && xrcc4PickupPrefab != null)
        {
            Vector3 center = GetDNACenter();
            Vector3 p1Snap = scaffoldStartPoint != null ? scaffoldStartPoint.position : manager.LeftDNAEnd.position;
            Vector3 p2Snap = scaffoldEndPoint   != null ? scaffoldEndPoint.position   : manager.RightDNAEnd.position;

            xrcc4Object1 = SpawnPickupProtein(center, 1, p1Snap);
            xrcc4Object2 = SpawnPickupProtein(center, 2, p2Snap);
        }
    }

    NetworkObject SpawnPickupProtein(Vector3 spawnPos, int role, Vector3 snapPos)
    {
        var go = Instantiate(xrcc4PickupPrefab, spawnPos, Quaternion.identity);
        go.GetComponent<ProteinOrbitController>()?.Configure(role, snapPos);
        var no = go.GetComponent<NetworkObject>();
        no?.Spawn();
        return no;
    }

    IEnumerator BuildScaffoldAndAlign()
    {
        // Build the full XRCC4/XLF scaffold bridge locally.
        if (xrcc4XlfSegmentPrefab != null && scaffoldSegmentCount > 0)
        {
            Vector3 start = scaffoldStartPoint != null ? scaffoldStartPoint.position : manager.LeftDNAEnd.position;
            Vector3 end   = scaffoldEndPoint   != null ? scaffoldEndPoint.position   : manager.RightDNAEnd.position;
            float segDelay = 0.3f;

            for (int i = 0; i < scaffoldSegmentCount; i++)
            {
                float t   = scaffoldSegmentCount > 1 ? (float)i / (scaffoldSegmentCount - 1) : 0.5f;
                Vector3 pos = Vector3.Lerp(start, end, t);
                var seg     = Instantiate(xrcc4XlfSegmentPrefab, pos + Vector3.up * 0.5f, Quaternion.identity);
                scaffoldSegments.Add(seg);

                var npc = seg.GetComponent<NHEJProteinNPC>();
                if (npc != null) StartCoroutine(npc.MoveToTarget(pos, segDelay * 0.8f));

                yield return new WaitForSeconds(segDelay);
            }

            if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlaySnap();
        }

        // Pull DNA ends together.
        if (manager.LeftDNAEnd  != null && leftEndAlignTarget  != null)
            StartCoroutine(MoveTransform(manager.LeftDNAEnd,  leftEndAlignTarget.position,  alignDuration));
        if (manager.RightDNAEnd != null && rightEndAlignTarget != null)
            StartCoroutine(MoveTransform(manager.RightDNAEnd, rightEndAlignTarget.position, alignDuration));
    }

    IEnumerator WaitThenAdvance(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (manager != null && manager.IsServer)
            manager.AdvancePhase();
    }

    IEnumerator MoveTransform(Transform t, Vector3 target, float duration)
    {
        Vector3 start = t.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            t.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        t.position = target;
    }

    Vector3 GetDNACenter()
    {
        if (manager.LeftDNAEnd != null && manager.RightDNAEnd != null)
            return (manager.LeftDNAEnd.position + manager.RightDNAEnd.position) * 0.5f;
        return manager.LeftDNAEnd != null ? manager.LeftDNAEnd.position : Vector3.zero;
    }
}
