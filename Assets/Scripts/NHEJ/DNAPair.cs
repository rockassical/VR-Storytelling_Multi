using UnityEngine;

// Attach to every DNAWall-tagged segment on DNA_ChunkyCuts.
//
// At Start, each wall finds its nearest neighbour (also a DNAPair) within autoPairRadius
// and caches it as "partner". The two walls represent the matching segments on the two
// strands of the double helix at the same position.
//
// NHEJBreakPoint calls CheckAfterDelay() on every surviving wall once the explosion
// destroy-delay has elapsed. At that point, any wall whose partner was destroyed glows red —
// that wall is an exposed overhang (Artemis must cut it).
//
// IMPORTANT — tuning autoPairRadius:
//   The radius must be SMALLER than the along-axis spacing between adjacent walls, otherwise
//   AutoPair will pick an axis-neighbor instead of the cross-strand partner. Select a wall in
//   the Scene view; the gizmo sphere shows the search radius. Shrink it until it only reaches
//   the wall directly across the helix, not the one beside it along the axis.
//
// Setup:
//   - Add this component to every wall object in DNA_ChunkyCuts (or the prefab).
//   - Tune autoPairRadius (see above).
//   - Set overhangMaterial to the same red/orange emissive used elsewhere for overhangs.
//   - Partners can also be assigned manually by dragging into the Partner slot.
public class DNAPair : MonoBehaviour
{
    [Tooltip("The matching wall on the opposite strand. Assigned automatically at Start if left empty.")]
    [SerializeField] DNAPair partner;

    [Tooltip("World-space search radius for auto-pairing. Must be smaller than the along-axis wall spacing — tune with the gizmo sphere in Scene view.")]
    [SerializeField] float autoPairRadius = 1f;

    [Tooltip("Material applied to this wall when its partner is destroyed (overhang state).")]
    [SerializeField] Material overhangMaterial;

    bool glowing;
    Renderer[] renderers;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();

        if (partner == null)
            AutoPair();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>True while this wall is glowing as an exposed overhang.</summary>
    public bool IsOverhang => glowing;

    /// <summary>The paired wall on the opposite strand (null if unpaired or destroyed).</summary>
    public DNAPair Partner => partner;

    /// <summary>
    /// Called by NHEJBreakPoint after the explosion destroy-delay has elapsed.
    /// Waits an additional 'delay' seconds, then checks whether the partner was destroyed.
    /// Only glows if this wall had a partner and that partner is now gone.
    /// </summary>
    public void CheckAfterDelay(float delay)
    {
        if (partner == null) return;  // never had a partner — not an overhang candidate
        Invoke(nameof(DoCheck), delay);
    }

    void DoCheck()
    {
        // Unity fake-null: Destroy()ed objects compare == null.
        if (partner == null)
            SetGlow(true);
    }

    /// <summary>
    /// Force a re-pair search. Useful if walls are spawned at runtime or the
    /// hierarchy changes before Start() runs.
    /// </summary>
    public void AutoPair()
    {
        float nearest = float.MaxValue;
        DNAPair best  = null;

        foreach (var dp in FindObjectsOfType<DNAPair>())
        {
            if (dp == this) continue;

            // If another wall already points to us as its partner, accept that pairing.
            if (dp.partner == this)
            {
                partner = dp;
                return;
            }

            float d = Vector3.Distance(transform.position, dp.transform.position);
            if (d < nearest && d <= autoPairRadius)
            {
                nearest = d;
                best    = dp;
            }
        }

        partner = best;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    void SetGlow(bool on)
    {
        glowing = on;
        if (overhangMaterial == null) return;
        foreach (var r in renderers)
            if (r != null) r.material = overhangMaterial;
    }

    // ── Editor gizmos ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Yellow line to partner.
        if (partner != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, partner.transform.position);
        }

        // Auto-pair search radius.
        Gizmos.color = new Color(1f, 1f, 0f, 0.12f);
        Gizmos.DrawWireSphere(transform.position, autoPairRadius);
    }
#endif
}
