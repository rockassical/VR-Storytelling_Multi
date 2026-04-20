using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Phase4_GapFill : NHEJPhaseHandler
{
    [Header("Gap Segment Prefab (spawned at runtime per gap slot)")]
    [Tooltip("DNAWallSegment prefab — one instance spawned per explosion gap slot.")]
    [SerializeField] GameObject gapSegmentPrefab;
    [Tooltip("How far above and to the side of the gap slot to scatter spawned segments.")]
    [SerializeField] float spawnScatterRadius = 0.25f;

    [Header("LigaseIV Spray Can")]
    [Tooltip("Prefab with LigaseSprayCan + NetworkObject. Must be in NetworkPrefabs list.")]
    [SerializeField] GameObject ligaseSprayCanPrefab;
    [Tooltip("Where spray cans are spawned. One can per point. Leave empty for a single can at DNA centre.")]
    [SerializeField] Transform[] sprayCanSpawnPoints;

    [Header("Timing")]
    [SerializeField] float completionPause = 1.2f;

    public override bool IsAutomatic => false;

    readonly List<NetworkObject> spawnedSprayCans = new();
    readonly List<DNAWallSegment> spawnedSegments = new();
    int sealedCount;
    float totalScore;
    bool phaseEnded;

    // ── NHEJPhaseHandler ──────────────────────────────────────────────────────

    public override void Setup()
    {
        sealedCount = 0;
        totalScore  = 0f;
        phaseEnded  = false;
        spawnedSegments.Clear();
    }

    public override void StartPhase()
    {
        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayPhaseAdvance();

        // Show gap-fill glow indicators on all clients.
        manager.BreakPoint?.ShowGapFillGlows();

        if (manager.IsServer)
            StartCoroutine(RunPhase());
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        manager.BreakPoint?.HideGapFillGlows();

        if (manager.IsServer)
        {
            foreach (var no in spawnedSprayCans)
                if (no != null && no.IsSpawned) no.Despawn();
            spawnedSprayCans.Clear();

            foreach (var seg in spawnedSegments)
                if (seg != null && seg.NetworkObject != null && seg.NetworkObject.IsSpawned)
                    seg.NetworkObject.Despawn();
            spawnedSegments.Clear();
        }
    }

    // ── Server orchestration ──────────────────────────────────────────────────

    IEnumerator RunPhase()
    {
        // Small pause so glow indicators register before segments appear.
        yield return new WaitForSeconds(0.5f);

        SpawnGapSegments();
        SpawnSprayCans();
    }

    // ── Gap Segment Spawning ──────────────────────────────────────────────────

    void SpawnGapSegments()
    {
        if (gapSegmentPrefab == null || manager.BreakPoint == null)
        {
            Debug.LogWarning("[NHEJ Phase4] gapSegmentPrefab or BreakPoint not assigned.");
            return;
        }

        var positions = manager.BreakPoint.GetGapFillPositions();
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 gapPos = positions[i];

            // Scatter spawn position near the gap so players have to move it back.
            Vector3 scatter = new Vector3(
                UnityEngine.Random.Range(-spawnScatterRadius, spawnScatterRadius),
                UnityEngine.Random.Range(0.1f, spawnScatterRadius),
                UnityEngine.Random.Range(-spawnScatterRadius, spawnScatterRadius));
            Vector3 spawnPos = gapPos + scatter;

            var go  = Instantiate(gapSegmentPrefab, spawnPos, Quaternion.identity);
            var no  = go.GetComponent<NetworkObject>();
            var seg = go.GetComponent<DNAWallSegment>();
            no?.Spawn();

            if (seg != null && no != null)
            {
                seg.SetCorrectPosition(gapPos);
                spawnedSegments.Add(seg);
            }
        }

        Debug.Log($"[NHEJ Phase4] Spawned {positions.Count} gap segment(s).");
    }

    // ── Spray Can Spawning ────────────────────────────────────────────────────

    void SpawnSprayCans()
    {
        if (ligaseSprayCanPrefab == null) return;

        int count = sprayCanSpawnPoints != null && sprayCanSpawnPoints.Length > 0
            ? sprayCanSpawnPoints.Length : 1;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = sprayCanSpawnPoints != null && i < sprayCanSpawnPoints.Length
                ? sprayCanSpawnPoints[i].position
                : GetDNACenter() + Vector3.up * 0.3f + Vector3.right * (i * 0.3f);

            var go = Instantiate(ligaseSprayCanPrefab, pos, Quaternion.identity);
            var no = go.GetComponent<NetworkObject>();
            no?.Spawn();
            if (no != null) spawnedSprayCans.Add(no);
        }

        Debug.Log($"[NHEJ Phase4] Spawned {count} LigaseSprayCan(s).");
    }

    // ── Called by LigaseSprayCan (server-side) ────────────────────────────────

    /// <summary>Server-only. Called by LigaseSprayCan.RequestSealServerRpc when a segment is sealed.</summary>
    public void OnSegmentSealed(DNAWallSegment segment, float score)
    {
        if (!manager.IsServer || phaseEnded) return;

        sealedCount++;
        totalScore += Mathf.Clamp01(score);

        int total = spawnedSegments.Count;
        Debug.Log($"[NHEJ Phase4] Segment sealed — score={score:P0} ({sealedCount}/{total})");

        if (sealedCount >= total)
            StartCoroutine(FinishPhase());
    }

    IEnumerator FinishPhase()
    {
        phaseEnded = true;
        yield return new WaitForSeconds(completionPause);

        int total = spawnedSegments.Count;
        float avgScore = total > 0 ? (totalScore / total) * 100f : 100f;

        manager.ReportGapFillScore(avgScore);
        manager.AdvancePhase();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    Vector3 GetDNACenter()
    {
        if (manager.LeftDNAEnd != null && manager.RightDNAEnd != null)
            return (manager.LeftDNAEnd.position + manager.RightDNAEnd.position) * 0.5f;
        return manager.LeftDNAEnd?.position ?? Vector3.zero;
    }
}
