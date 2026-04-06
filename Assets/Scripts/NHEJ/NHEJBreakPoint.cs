using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Attach to the DNA_ChunkyCuts root GameObject in the scene.
//
// DNA_ChunkyCuts is a flat hierarchy: the root has many direct children arranged in
// groups of 3 (one DNAWall-tagged segment + two nucleotides). Set segmentHierarchyDepth = 1.
//
// At phase start, GenerateBreak(seed) is called on ALL clients with the same seed.
// It:
//   1. Sorts all direct children along the helix axis.
//   2. Picks a random break center (not near edges).
//   3. Categorises segments: clean | left-overhang | explosion | right-overhang | clean
//   4. Launches explosion segments with physics (VFX only — each client runs independently).
//      Because nucleotides are siblings of walls (same depth), everything in the axis range
//      explodes together — the full group of 3 flies at once.
//   5. Applies an overhang glow material to the overhang segments ("cut me").
//   6. Spawns seam indicators at the exact cut lines (where Artemis should slice).
//   7. Creates BoxCollider OverhangZone triggers for ArtemisBlade detection.
//
// CutOverhang(playerRole, bladeTip) is called on ALL clients from NHEJManager.CutConfirmedClientRpc.
//   - Hides the overhang segments for that side.
//   - Removes the seam indicator and OverhangZone trigger for that side.
//
// ShowGapFillGlows() / HideGapFillGlows() show/hide placement indicators at gap positions.
// GetGapFillPositions() returns world positions for Phase4 to spawn gap-fill pieces.
//
// Scene setup (inspector):
//   - helixAxisLocal: the LOCAL axis the helix runs along. Check in Scene view — flip or negate if sorting looks wrong.
//   - overhangGlowMaterial: a bright red/orange emissive material — applied to overhang segments.
//   - seamIndicatorPrefab: a glowing ring/disc prefab placed at the seam cut line.
//   - gapFillIndicatorPrefab: a glowing outline prefab placed at each gap position in Phase4.
public class NHEJBreakPoint : MonoBehaviour
{
    [Header("Helix Axis")]
    [Tooltip("Local-space long axis of the helix. Check Scene view to see which direction the helix runs.")]
    [SerializeField] Vector3 helixAxisLocal = Vector3.right;
    [Tooltip("Tick if segments sort in reverse order (wrong end first). Negates the sort direction.")]
    [SerializeField] bool reverseSort = false;
    [Tooltip("How many levels deep to collect segments. 1 = direct children (use for DNA_ChunkyCuts flat hierarchy), 2 = grandchildren (use if the root has group objects as direct children).")]
    [SerializeField] int segmentHierarchyDepth = 1;

    [Tooltip("Only GameObjects with this tag are treated as wall segments. Nucleotides and other types are ignored.")]
    [SerializeField] string wallTag = "DNAWall";

    [Header("Break Parameters")]
    [Range(0.05f, 0.35f)]
    [Tooltip("Fraction of total segments to exclude at each end. Prevents breaks too close to the tips.")]
    [SerializeField] float edgeExclusionFraction = 0.15f;
    [Tooltip("How many segments get blown away (the gap players must fill).")]
    [SerializeField] int explosionSegmentCount = 4;
    [Tooltip("How many extra segments remain on each side as overhangs (must be cut by Artemis).")]
    [SerializeField] int overhangSegmentCount = 2;

    [Header("Explosion VFX")]
    [SerializeField] float explosionForce       = 1.0f;
    [SerializeField] float explosionUpBias      = 0.8f;    // extra upward push on each piece
    [SerializeField] float explosionDestroyDelay = 3f;
    [SerializeField] float explosionStaggerDelay = 0.07f;  // seconds between each piece launch
    [Tooltip("How many extra segment-spacings beyond the explosion zone to scan for floating nucleotide cleanup.")]
    [SerializeField] float floatingCleanupPadMultiplier = 10f;

    [Header("Overhang Glow — Artemis Target")]
    [Tooltip("Material swapped onto overhang segments so they glow red/orange ('cut me').")]
    [SerializeField] Material overhangGlowMaterial;
    [Tooltip("Optional prefab (e.g. glowing ring) placed at the exact seam — the cut target.")]
    [SerializeField] GameObject seamIndicatorPrefab;

    [Header("Gap Fill Glow — LigaseIV Target")]
    [Tooltip("Optional prefab placed at each gap position when Phase 4 starts, showing where to place pieces.")]
    [SerializeField] GameObject gapFillIndicatorPrefab;

    // ── Segment layout ────────────────────────────────────────────────────────
    //
    // Indices are used only to CHOOSE zone boundaries from the sorted wall-type sample.
    // All per-segment operations (explode, glow, hide) use axis-projection RANGES so that
    // every segment type (walls, nucleotides, backbone) at the same physical column is caught.
    //
    List<Transform> sortedSegments = new();
    int explosionStart;
    int explosionEnd;

    // Axis-projection range for the explosion zone only (used by ExplodeSegments).
    float explosionAxisMin, explosionAxisMax;

    // Overhang segments detected by neighbor-count change after the explosion zone is chosen.
    // These are the ONLY walls glowed and hidden — nothing else is touched.
    readonly List<Transform> leftOverhangSegs  = new();
    readonly List<Transform> rightOverhangSegs = new();

    // All segments at the configured depth — walls + nucleotides. Used only for explosion.
    readonly List<Transform> allSegments = new();

    // Gap positions cached before explosion destroys those GameObjects.
    readonly List<Vector3> cachedGapPositions = new();

    float segmentSpacing;
    Vector3 helixAxisWorld;
    Vector3 axisOrigin;

    Vector3 leftSeamPos;   // world pos of ideal left cut
    Vector3 rightSeamPos;  // world pos of ideal right cut

    float leftCutScore  = -1f;  // -1 = not yet cut
    float rightCutScore = -1f;

    GameObject leftZoneGO;
    GameObject rightZoneGO;

    readonly List<GameObject> seamIndicators    = new();
    readonly List<GameObject> gapFillIndicators = new();

    // ── Public properties / events ────────────────────────────────────────────

    /// <summary>
    /// Fires on all clients after the explosion destroy-delay has elapsed and all DNAPair
    /// checks have run. bool = true if at least one wall is an overhang (glowing red).
    /// Phase3_Trimming uses this to skip Artemis if the explosion left no overhangs.
    /// </summary>
    public event Action<bool> OnOverhangsResolved;

    public float AverageTrimScore =>
        ((leftCutScore  >= 0 ? leftCutScore  : 1f) +
         (rightCutScore >= 0 ? rightCutScore : 1f)) * 0.5f;

    public int GapSegmentCount => cachedGapPositions.Count > 0
        ? cachedGapPositions.Count
        : Mathf.Max(0, explosionEnd - explosionStart);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called on ALL clients (via NHEJManager.GenerateBreakClientRpc) with the same seed.
    /// Deterministically generates the break.
    /// </summary>
    public void GenerateBreak(int seed)
    {
        CollectAndSortSegments();
        int n = sortedSegments.Count;

        int minNeeded = explosionSegmentCount + Mathf.RoundToInt(n * edgeExclusionFraction) * 2 + 4;
        if (n < minNeeded)
        {
            Debug.LogError($"[NHEJBreakPoint] Only {n} segments — need at least {minNeeded} " +
                           "for current settings. Reduce explosion counts or edgeExclusion.");
            return;
        }

        helixAxisWorld = transform.TransformDirection(helixAxisLocal).normalized;
        axisOrigin     = sortedSegments[0].position;
        segmentSpacing = Vector3.Distance(sortedSegments[0].position,
                                          sortedSegments[n - 1].position) / (n - 1);

        int edge      = Mathf.RoundToInt(n * edgeExclusionFraction);
        int halfExp   = explosionSegmentCount / 2;
        int centerMin = edge + halfExp + 1;
        int centerMax = n - edge - (explosionSegmentCount - halfExp) - 1;

        if (centerMax <= centerMin)
        {
            Debug.LogError("[NHEJBreakPoint] centerMax <= centerMin — helix may be too short.");
            return;
        }

        int center = new System.Random(seed).Next(centerMin, centerMax);

        explosionStart = center - halfExp;
        explosionEnd   = explosionStart + explosionSegmentCount;

        float pad = segmentSpacing;
        explosionAxisMin = Proj(sortedSegments[explosionStart].position)   - pad;
        explosionAxisMax = Proj(sortedSegments[explosionEnd - 1].position) + pad;

        // Cache gap positions before those GameObjects are destroyed.
        cachedGapPositions.Clear();
        for (int i = explosionStart; i < explosionEnd; i++)
            cachedGapPositions.Add(sortedSegments[i].position);

        Debug.Log($"[NHEJBreakPoint] seed={seed} n={n} center={center} " +
                  $"gap=[{explosionStart},{explosionEnd})");

        // Detect which walls drop from 2→1 neighbors after the explosion, glow + set seam positions.
        DetectAndGlowOverhangs();
        SpawnSeamIndicators();
        CreateOverhangZones();
        StartCoroutine(ExplodeSegments());
    }

    /// <summary>
    /// Read-only score computation — called server-side in ReportCutServerRpc.
    /// Does NOT modify any state.
    /// </summary>
    public float ComputeTrimScore(int playerRole, Vector3 bladeTipWorldPos)
    {
        Vector3 seamPos = playerRole == 1 ? leftSeamPos : rightSeamPos;
        float seamProj  = Proj(seamPos);
        float bladeProj = Proj(bladeTipWorldPos);
        float tolerance = Mathf.Max(segmentSpacing * 2f, 0.001f);
        return 1f - Mathf.Clamp01(Mathf.Abs(bladeProj - seamProj) / tolerance);
    }

    /// <summary>
    /// Applies the cut visually on ALL clients (called from NHEJManager.CutConfirmedClientRpc).
    /// Hides overhang segments, removes zone trigger and seam indicator for that side.
    /// </summary>
    public void CutOverhang(int playerRole, Vector3 bladeTipWorldPos)
    {
        if (playerRole == 1)
        {
            foreach (var seg in leftOverhangSegs)
                if (seg != null) HideSegment(seg);
            DestroyAndNull(ref leftZoneGO);
            RemoveSeamIndicator(0);
            leftCutScore = ComputeTrimScore(1, bladeTipWorldPos);
        }
        else
        {
            foreach (var seg in rightOverhangSegs)
                if (seg != null) HideSegment(seg);
            DestroyAndNull(ref rightZoneGO);
            RemoveSeamIndicator(1);
            rightCutScore = ComputeTrimScore(2, bladeTipWorldPos);
        }
    }

    /// <summary>Show glow indicators at each gap position. Called at Phase 4 start (all clients).</summary>
    public void ShowGapFillGlows()
    {
        HideGapFillGlows();
        if (gapFillIndicatorPrefab == null) return;
        foreach (var pos in cachedGapPositions)
        {
            var go = Instantiate(gapFillIndicatorPrefab, pos, Quaternion.identity);
            gapFillIndicators.Add(go);
        }
    }

    /// <summary>Remove gap fill glow indicators. Called at Phase 4 complete.</summary>
    public void HideGapFillGlows()
    {
        foreach (var g in gapFillIndicators) if (g != null) Destroy(g);
        gapFillIndicators.Clear();
    }

    /// <summary>World positions for each gap slot — used by Phase4_GapFill to spawn pieces.</summary>
    public List<Vector3> GetGapFillPositions() => new List<Vector3>(cachedGapPositions);

    // ── Private helpers ───────────────────────────────────────────────────────

    void CollectAndSortSegments()
    {
        sortedSegments.Clear();
        CollectAtDepth(transform, segmentHierarchyDepth);

        // Keep a full copy (walls + nucleotides) for explosion — everything in the gap flies.
        allSegments.Clear();
        allSegments.AddRange(sortedSegments);

        // Strip non-walls so only DNAWall segments are used for glow/cut/zone logic.
        if (!string.IsNullOrEmpty(wallTag))
            sortedSegments.RemoveAll(t => t == null || !t.CompareTag(wallTag));

        Vector3 axis   = transform.TransformDirection(helixAxisLocal).normalized;
        Vector3 origin = sortedSegments.Count > 0 ? sortedSegments[0].position : Vector3.zero;
        int     dir    = reverseSort ? -1 : 1;

        sortedSegments.Sort((a, b) =>
            (dir * Vector3.Dot(a.position - origin, axis))
            .CompareTo(dir * Vector3.Dot(b.position - origin, axis)));
    }

    void CollectAtDepth(Transform parent, int depth)
    {
        foreach (Transform child in parent)
        {
            if (child == null) continue;
            if (depth <= 1)
                sortedSegments.Add(child);
            else
                CollectAtDepth(child, depth - 1);
        }
    }

    float Proj(Vector3 worldPos) => Vector3.Dot(worldPos - axisOrigin, helixAxisWorld);

    static Vector3 Midpoint(Transform a, Transform b) => (a.position + b.position) * 0.5f;

    /// <summary>
    /// For each surviving wall, counts how many adjacent walls it had BEFORE the explosion
    /// vs. how many it has AFTER (excluding the explosion zone). Any wall that drops from
    /// 2 neighbors to 1 is an exposed overhang — glow it red and record it.
    /// Seam positions are set from the detected segments.
    /// </summary>
    void DetectAndGlowOverhangs()
    {
        leftOverhangSegs.Clear();
        rightOverhangSegs.Clear();

        // Two walls are "neighbors" if they are within 1.5× the average spacing along the axis.
        float threshold = segmentSpacing * 1.5f;

        for (int i = 0; i < sortedSegments.Count; i++)
        {
            // Skip explosion zone segments (they're being destroyed).
            if (i >= explosionStart && i < explosionEnd) continue;
            var seg = sortedSegments[i];
            if (seg == null) continue;

            float p = Proj(seg.position);

            // Neighbor to the left in the sorted array.
            bool leftExists   = i > 0 && sortedSegments[i - 1] != null &&
                                 Mathf.Abs(p - Proj(sortedSegments[i - 1].position)) <= threshold;
            bool leftSurvives = leftExists && (i - 1 < explosionStart || i - 1 >= explosionEnd);

            // Neighbor to the right in the sorted array.
            bool rightExists   = i < sortedSegments.Count - 1 && sortedSegments[i + 1] != null &&
                                  Mathf.Abs(p - Proj(sortedSegments[i + 1].position)) <= threshold;
            bool rightSurvives = rightExists && (i + 1 < explosionStart || i + 1 >= explosionEnd);

            int before = (leftExists  ? 1 : 0) + (rightExists  ? 1 : 0);
            int after  = (leftSurvives ? 1 : 0) + (rightSurvives ? 1 : 0);

            // Had 2 neighbors before, only 1 after → exposed end next to the gap.
            if (before == 2 && after == 1)
            {
                if (overhangGlowMaterial != null)
                    SwapMaterial(seg, overhangGlowMaterial);

                if (i < explosionStart)
                    leftOverhangSegs.Add(seg);
                else
                    rightOverhangSegs.Add(seg);
            }
        }

        // Seam = position of the detected overhang seg (the exposed wall next to the gap).
        if (leftOverhangSegs.Count > 0)
            leftSeamPos  = leftOverhangSegs[leftOverhangSegs.Count - 1].position;
        if (rightOverhangSegs.Count > 0)
            rightSeamPos = rightOverhangSegs[0].position;

        Debug.Log($"[NHEJBreakPoint] Overhang segs — left:{leftOverhangSegs.Count} right:{rightOverhangSegs.Count}");
    }

    bool InAxisRange(Transform t, float min, float max)
    {
        if (t == null) return false;
        float p = Proj(t.position);
        return p >= min && p <= max;
    }

    void SwapMaterial(Transform seg, Material mat)
    {
        foreach (var rend in seg.GetComponentsInChildren<Renderer>())
            rend.material = mat;
    }

    void HideSegment(Transform seg)
    {
        foreach (var rend in seg.GetComponentsInChildren<Renderer>())
            rend.enabled = false;
        foreach (var col in seg.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    void SpawnSeamIndicators()
    {
        if (seamIndicatorPrefab == null) return;
        seamIndicators.Add(Instantiate(seamIndicatorPrefab, leftSeamPos,  Quaternion.identity));
        seamIndicators.Add(Instantiate(seamIndicatorPrefab, rightSeamPos, Quaternion.identity));
    }

    void RemoveSeamIndicator(int idx)
    {
        if (idx < seamIndicators.Count && seamIndicators[idx] != null)
        {
            Destroy(seamIndicators[idx]);
            seamIndicators[idx] = null;
        }
    }

    void CreateOverhangZones()
    {
        leftZoneGO  = MakeZoneFromSegs(leftOverhangSegs,  1);
        rightZoneGO = MakeZoneFromSegs(rightOverhangSegs, 2);
    }

    GameObject MakeZoneFromSegs(List<Transform> segs, int playerRole)
    {
        if (segs.Count == 0) return null;

        Vector3 centre = Vector3.zero;
        foreach (var s in segs) centre += s.position;
        centre /= segs.Count;

        var go = new GameObject($"OverhangZone_P{playerRole}");
        go.transform.position = centre;

        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;

        // Cover all detected overhang segs plus clearance for VR swings.
        float axisLen = Mathf.Max(segs.Count * segmentSpacing, segmentSpacing) + segmentSpacing * 0.5f;
        col.size = new Vector3(
            Mathf.Abs(helixAxisWorld.x) * axisLen + 0.2f,
            0.35f,
            Mathf.Abs(helixAxisWorld.z) * axisLen + 0.2f
        );

        var zone = go.AddComponent<OverhangZone>();
        zone.playerRole = playerRole;
        return go;
    }

    static void DestroyAndNull(ref GameObject go)
    {
        if (go != null) { Destroy(go); go = null; }
    }

    IEnumerator ExplodeSegments()
    {
        foreach (Transform seg in allSegments)
        {
            if (seg == null || !InAxisRange(seg, explosionAxisMin, explosionAxisMax)) continue;

            seg.SetParent(null);  // detach so only this segment flies

            Rigidbody rb = seg.GetComponent<Rigidbody>();
            if (rb == null) rb = seg.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity  = true;

            // Launch outward from DNA centre + random spread
            Vector3 outward   = (seg.position - transform.position).normalized;
            Vector3 launchDir = (outward + Vector3.up * explosionUpBias + UnityEngine.Random.insideUnitSphere * 0.3f).normalized;
            rb.AddForce(launchDir * explosionForce, ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 2f, ForceMode.Impulse);

            Destroy(seg.gameObject, explosionDestroyDelay);

            if (explosionStaggerDelay > 0f)
                yield return new WaitForSeconds(explosionStaggerDelay);
        }

        // ── Nucleotide floating cleanup ───────────────────────────────────────
        // Non-wall segments near the zone edge that lost their wall neighbours self-eject.
        float cleanupPad = segmentSpacing * floatingCleanupPadMultiplier;
        float cleanupMin = explosionAxisMin - cleanupPad;
        float cleanupMax = explosionAxisMax + cleanupPad;

        foreach (var seg in allSegments)
        {
            if (seg == null) continue;
            if (!InAxisRange(seg, cleanupMin, cleanupMax)) continue;

            // Walls are managed by DNAPair — skip them here.
            if (!string.IsNullOrEmpty(wallTag) && seg.CompareTag(wallTag)) continue;

            var cleanup = seg.GetComponent<DNAFloatingCleanup>();
            if (cleanup == null) cleanup = seg.gameObject.AddComponent<DNAFloatingCleanup>();
            cleanup.Activate();
        }

        // ── DNAPair overhang check ────────────────────────────────────────────
        // Wait until the exploded pieces are fully destroyed, then ask every surviving
        // wall to check whether its partner is still alive. Walls whose partner was in
        // the explosion zone will glow red (exposed overhang).
        float pairCheckDelay = explosionDestroyDelay + 0.2f;

        var survivingPairs = new List<DNAPair>();
        foreach (var seg in allSegments)
        {
            if (seg == null) continue;
            if (InAxisRange(seg, explosionAxisMin, explosionAxisMax)) continue; // was exploded
            if (string.IsNullOrEmpty(wallTag) || !seg.CompareTag(wallTag)) continue;

            var pair = seg.GetComponent<DNAPair>();
            if (pair != null)
            {
                survivingPairs.Add(pair);
                pair.CheckAfterDelay(pairCheckDelay);
            }
        }

        // Fire OnOverhangsResolved one frame after the pair checks have had time to run.
        StartCoroutine(NotifyOverhangResult(survivingPairs, pairCheckDelay + 0.1f));
    }

    IEnumerator NotifyOverhangResult(List<DNAPair> pairs, float delay)
    {
        yield return new WaitForSeconds(delay);

        bool any = false;
        foreach (var p in pairs)
        {
            if (p != null && p.IsOverhang) { any = true; break; }
        }

        OnOverhangsResolved?.Invoke(any);
        Debug.Log($"[NHEJBreakPoint] OnOverhangsResolved — hasOverhangs={any}");
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (sortedSegments == null || sortedSegments.Count == 0) return;
        if (leftOverhangSegs.Count == 0 && rightOverhangSegs.Count == 0) return;

        // Left seam — green
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(leftSeamPos, 0.03f);

        // Right seam — cyan
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(rightSeamPos, 0.03f);
    }
#endif
}
