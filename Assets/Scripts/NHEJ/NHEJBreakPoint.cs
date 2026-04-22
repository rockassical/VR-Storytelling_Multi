using System;
using System.Collections.Generic;
using UnityEngine;

// Attach to the DNA_ChunkyCuts root GameObject.
//
// The DNA gap is already present in the scene (pieces manually removed before play).
// On GenerateBreak(), this script:
//   1. Collects and sorts all wall segments along the helix axis.
//   2. Finds walls with a neighbor on one side but not the other — these are overhangs.
//   3. Glows those walls red and spawns seam indicators at their positions.
//   4. Creates OverhangZone triggers for Artemis to detect.
//   5. Fires OnOverhangsResolved so the phase manager knows whether Artemis is needed.
public class NHEJBreakPoint : MonoBehaviour
{
    [Header("Helix Axis")]
    [SerializeField] Vector3 helixAxisLocal = Vector3.right;
    [SerializeField] bool reverseSort = false;
    [SerializeField] int segmentHierarchyDepth = 1;
    [SerializeField] string wallTag = "DNAWall";

    [Header("Overhang — Artemis Target")]
    [SerializeField] GameObject seamIndicatorPrefab;

    [Header("Gap Fill Glow — Phase 4")]
    [SerializeField] GameObject gapFillIndicatorPrefab;
    [Tooltip("Number of gap fill slots to interpolate between the two seam positions.")]
    [SerializeField] int gapFillSlotCount = 4;

    [Header("Bridge Anchors — assign the 4 DNASealPoints at the gap edges")]
    [Tooltip("Max distance from the BFS frontier to the goal anchor that counts as bridged.")]
    [SerializeField] float bridgeCompletionRadius = 0.5f;
    [Tooltip("Left edge, strand 1 (top).")]
    [SerializeField] DNASealPoint leftAnchorStrand1;
    [Tooltip("Left edge, strand 2 (bottom).")]
    [SerializeField] DNASealPoint leftAnchorStrand2;
    [Tooltip("Right edge, strand 1 (top).")]
    [SerializeField] DNASealPoint rightAnchorStrand1;
    [Tooltip("Right edge, strand 2 (bottom).")]
    [SerializeField] DNASealPoint rightAnchorStrand2;

    // ── Runtime state ─────────────────────────────────────────────────────────

    List<Transform> sortedSegments = new();

    readonly List<Transform> leftOverhangSegs  = new();
    readonly List<Transform> rightOverhangSegs = new();

    float   segmentSpacing;
    Vector3 helixAxisWorld;
    Vector3 axisOrigin;
    Vector3 leftSeamPos;
    Vector3 rightSeamPos;

    float leftCutScore  = -1f;
    float rightCutScore = -1f;

    GameObject leftZoneGO;
    GameObject rightZoneGO;

    readonly List<GameObject> seamIndicators    = new();
    readonly List<GameObject> gapFillIndicators = new();

    // ── Public events / properties ────────────────────────────────────────────

    public event Action<bool> OnOverhangsResolved;

    public float AverageTrimScore =>
        ((leftCutScore  >= 0 ? leftCutScore  : 1f) +
         (rightCutScore >= 0 ? rightCutScore : 1f)) * 0.5f;

    public int GapSegmentCount => gapFillSlotCount;

    // ── Public API ────────────────────────────────────────────────────────────

    public void GenerateBreak(int seed)
    {
        CollectAndSortSegments();

        int n = sortedSegments.Count;
        if (n < 2)
        {
            Debug.LogError($"[NHEJBreakPoint] Not enough wall segments ({n}).");
            return;
        }

        helixAxisWorld = transform.TransformDirection(helixAxisLocal).normalized;
        axisOrigin     = sortedSegments[0].position;
        segmentSpacing = Vector3.Distance(sortedSegments[0].position,
                                          sortedSegments[n - 1].position) / (n - 1);

        DetectAndGlowOverhangs();
        SpawnSeamIndicators();
        CreateOverhangZones();

        bool hasOverhangs = leftOverhangSegs.Count > 0 || rightOverhangSegs.Count > 0;
        OnOverhangsResolved?.Invoke(hasOverhangs);

        Debug.Log($"[NHEJBreakPoint] Ready — overhangs L:{leftOverhangSegs.Count} R:{rightOverhangSegs.Count}");
    }

    public float ComputeTrimScore(int playerRole, Vector3 bladeTipWorldPos)
    {
        Vector3 seamPos = playerRole == 1 ? leftSeamPos : rightSeamPos;
        float tolerance = Mathf.Max(segmentSpacing * 2f, 0.001f);
        return 1f - Mathf.Clamp01(Mathf.Abs(Proj(bladeTipWorldPos) - Proj(seamPos)) / tolerance);
    }

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

    public void ShowGapFillGlows()
    {
        HideGapFillGlows();
        if (gapFillIndicatorPrefab == null) return;
        foreach (var pos in GetGapFillPositions())
            gapFillIndicators.Add(Instantiate(gapFillIndicatorPrefab, pos, Quaternion.identity));
    }

    public void HideGapFillGlows()
    {
        foreach (var g in gapFillIndicators) if (g != null) Destroy(g);
        gapFillIndicators.Clear();
    }

    /// <summary>Evenly spaced positions across the gap, for Phase 4 to spawn fill pieces.</summary>
    public List<Vector3> GetGapFillPositions()
    {
        var positions = new List<Vector3>();
        for (int i = 0; i < gapFillSlotCount; i++)
        {
            float t = gapFillSlotCount > 1 ? i / (float)(gapFillSlotCount - 1) : 0.5f;
            positions.Add(Vector3.Lerp(leftSeamPos, rightSeamPos, t));
        }
        return positions;
    }

    /// <summary>
    /// Returns true when the gap is fully bridged on both strands.
    /// Uses graph connectivity: each strand must have an unbroken chain of sealed
    /// SpawnedDNAWalls from its left anchor DNASealPoint to its right anchor.
    /// Assign the four anchor fields in the inspector.
    /// </summary>
    public bool IsGapBridged()
    {
        if (leftAnchorStrand1  == null || leftAnchorStrand2  == null ||
            rightAnchorStrand1 == null || rightAnchorStrand2 == null)
        {
            Debug.LogWarning("[NHEJBreakPoint] Bridge anchors not assigned — cannot check IsGapBridged.");
            return false;
        }

        bool s1 = CanReach(leftAnchorStrand1, rightAnchorStrand1);
        bool s2 = CanReach(leftAnchorStrand2, rightAnchorStrand2);
        Debug.Log($"[NHEJBreakPoint] IsGapBridged — strand1:{s1}  strand2:{s2}");
        return s1 && s2;
    }

    /// <summary>
    /// BFS through the seal-point graph.
    /// Nodes  = DNASealPoints.
    /// Edges  = SpawnedDNAWall (connects sealedBy ↔ ownSealPoint).
    /// </summary>
    bool CanReach(DNASealPoint start, DNASealPoint goal)
    {
        if (start == goal) return true;

        // Goal must have at least one wall sealed onto it — someone physically reached it.
        if (goal.LeftSealedWall == null && goal.RightSealedWall == null)
        {
            Debug.Log($"[BFS] {goal.gameObject.name} has no sealed walls — not yet reached.");
            return false;
        }

        var visited = new System.Collections.Generic.HashSet<DNASealPoint>();
        var queue   = new System.Collections.Generic.Queue<DNASealPoint>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node == goal) return true;

            float dist = Vector3.Distance(node.transform.position, goal.transform.position);
            Debug.Log($"[BFS] At {node.gameObject.name}  dist-to-goal:{dist:F2}  L:{node.LeftSealedWall?.gameObject.name ?? "null"}  R:{node.RightSealedWall?.gameObject.name ?? "null"}");

            // Close enough to the goal — counts as bridged.
            if (dist <= bridgeCompletionRadius) return true;

            TryEnqueue(node.LeftSealedWall,  goal, visited, queue);
            TryEnqueue(node.RightSealedWall, goal, visited, queue);
        }

        Debug.Log($"[BFS] Could not reach {goal.gameObject.name} from {start.gameObject.name}");
        return false;
    }

    void TryEnqueue(SpawnedDNAWall wall, DNASealPoint goal,
        System.Collections.Generic.HashSet<DNASealPoint> visited,
        System.Collections.Generic.Queue<DNASealPoint> queue)
    {
        if (wall == null || wall.OwnSealPoint == null) return;
        var next = wall.OwnSealPoint;
        if (next == goal) { queue.Enqueue(next); return; }
        if (!visited.Contains(next)) { visited.Add(next); queue.Enqueue(next); }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    void CollectAndSortSegments()
    {
        sortedSegments.Clear();
        CollectAtDepth(transform, segmentHierarchyDepth);
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
            if (depth <= 1) sortedSegments.Add(child);
            else CollectAtDepth(child, depth - 1);
        }
    }

    float Proj(Vector3 worldPos) => Vector3.Dot(worldPos - axisOrigin, helixAxisWorld);

    void DetectAndGlowOverhangs()
    {
        leftOverhangSegs.Clear();
        rightOverhangSegs.Clear();

        // Ask every wall to check its DNAPair — walls with no partner glow themselves red.
        foreach (var seg in sortedSegments)
        {
            if (seg == null) continue;
            var pair = seg.GetComponent<DNAPair>();
            if (pair == null) continue;

            pair.CheckNow();

            if (!pair.IsOverhang) continue;

            // Determine which side of the gap this overhang is on by its axis position
            // relative to the midpoint of all sorted segments.
            float mid = Proj(sortedSegments[sortedSegments.Count / 2].position);
            if (Proj(seg.position) < mid)
                leftOverhangSegs.Add(seg);
            else
                rightOverhangSegs.Add(seg);
        }

        if (leftOverhangSegs.Count > 0)
            leftSeamPos  = leftOverhangSegs[leftOverhangSegs.Count - 1].position;
        if (rightOverhangSegs.Count > 0)
            rightSeamPos = rightOverhangSegs[0].position;

        Debug.Log($"[NHEJBreakPoint] Overhangs — L:{leftOverhangSegs.Count} R:{rightOverhangSegs.Count}");
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
        if (leftOverhangSegs.Count > 0)
            seamIndicators.Add(Instantiate(seamIndicatorPrefab, leftSeamPos,  Quaternion.identity));
        if (rightOverhangSegs.Count > 0)
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

        var go  = new GameObject($"OverhangZone_P{playerRole}");
        go.transform.position = centre;

        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;

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

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(leftSeamPos, 0.03f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(rightSeamPos, 0.03f);
    }
#endif
}
