using System.Collections.Generic;
using UnityEngine;

// Placed on (or as a child of) "DNA double stand with separate parts".
//
// At Start, scans ALL MeshRenderer children and projects each one's world-space centre
// onto the helix long axis (junction → overhang end). Renderers that project past the
// cut point are hidden when CutAt() is called — hierarchy organisation is irrelevant.
//
// A small proxy rigidbody is spawned at each hidden renderer so pieces appear to fly off.
//
// Scene setup (inspector):
//   1. Add an empty child "JunctionMarker"  — position it at the exact base of the overhang
//      (where the overhang meets the main double-strand).
//   2. Add an empty child "OverhangEndMarker" — position it at the tip of the overhang.
//   3. Optionally set cutFlyoffPrefab (a simple sphere/cube with a Renderer + no collider).
//      If null, a plain white cube is generated at runtime.
public class DNAHelixCutter : MonoBehaviour
{
    [Header("Axis Markers (set as child GameObjects)")]
    [Tooltip("Empty child at the base of the overhang — where it meets the main helix.")]
    [SerializeField] Transform junctionMarker;
    [Tooltip("Empty child at the far tip of the overhang.")]
    [SerializeField] Transform overhangEndMarker;

    [Header("Flyoff VFX")]
    [Tooltip("Small prefab (sphere/cube) spawned at each cut renderer to simulate flying off. Leave null for auto-generated cube.")]
    [SerializeField] GameObject cutFlyoffPrefab;
    [SerializeField] float flyoffForce      = 1.8f;
    [SerializeField] float flyoffUpForce    = 0.8f;
    [SerializeField] float flyoffDestroyDelay = 2.5f;
    [SerializeField] float flyoffScale      = 0.04f;

    // ── Internal data ─────────────────────────────────────────────────────────

    struct RendererEntry
    {
        public MeshRenderer renderer;
        public float        projection;   // along helix axis from junction (>0 = in overhang)
    }

    List<RendererEntry> overhangRenderers = new();
    Vector3 helixDirection;
    float   overhangLength;
    bool    hasCut;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        if (junctionMarker == null || overhangEndMarker == null)
        {
            Debug.LogError("[DNAHelixCutter] JunctionMarker and OverhangEndMarker must be assigned.", this);
            return;
        }

        helixDirection = (overhangEndMarker.position - junctionMarker.position).normalized;
        overhangLength  = Vector3.Distance(junctionMarker.position, overhangEndMarker.position);

        // Collect every MeshRenderer in children, project its centre onto the helix axis.
        foreach (var mr in GetComponentsInChildren<MeshRenderer>())
        {
            Vector3 centre = mr.bounds.center;
            float proj = Vector3.Dot(centre - junctionMarker.position, helixDirection);

            // Only keep renderers that are on the overhang side (proj > 0).
            // Add a small -0.02 threshold to avoid falsely including geometry right at the junction.
            if (proj > -0.02f)
            {
                overhangRenderers.Add(new RendererEntry { renderer = mr, projection = proj });
            }
        }

        Debug.Log($"[DNAHelixCutter] Found {overhangRenderers.Count} overhang renderers " +
                  $"(axis length {overhangLength:F3}m).");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called on ALL clients by NHEJManager.CutConfirmedClientRpc.
    /// Hides every overhang renderer whose projection exceeds the cut point,
    /// and spawns a small flyoff proxy at its location.
    /// </summary>
    /// <returns>Placement score 0–1 (1 = cut right at junction = perfect trim).</returns>
    public float CutAt(Vector3 worldBladeTipPosition)
    {
        if (hasCut) return 0f;
        hasCut = true;

        float cutProj = Vector3.Dot(worldBladeTipPosition - junctionMarker.position, helixDirection);
        // Clamp so we never cut behind the junction.
        cutProj = Mathf.Max(0f, cutProj);

        int hiddenCount = 0;

        foreach (var entry in overhangRenderers)
        {
            if (entry.projection > cutProj && entry.renderer != null)
            {
                SpawnFlyoff(entry.renderer.bounds.center);
                entry.renderer.enabled = false;
                hiddenCount++;
            }
        }

        Debug.Log($"[DNAHelixCutter] Cut at proj={cutProj:F3}m — hid {hiddenCount} renderers.");

        // Score: 1 when blade is right at junction; 0 when at overhang end.
        float score = 1f - Mathf.Clamp01(cutProj / Mathf.Max(overhangLength, 0.001f));
        return score;
    }

    /// <summary>Compute the cut score without actually modifying renderers (used server-side).</summary>
    public float ComputeScore(Vector3 worldBladeTipPosition)
    {
        float cutProj = Vector3.Dot(worldBladeTipPosition - junctionMarker.position, helixDirection);
        cutProj = Mathf.Max(0f, cutProj);
        return 1f - Mathf.Clamp01(cutProj / Mathf.Max(overhangLength, 0.001f));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SpawnFlyoff(Vector3 worldPos)
    {
        GameObject proxy;
        if (cutFlyoffPrefab != null)
        {
            proxy = Instantiate(cutFlyoffPrefab, worldPos, Random.rotation);
        }
        else
        {
            proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            proxy.transform.position = worldPos;
            proxy.transform.rotation = Random.rotation;
            proxy.transform.localScale = Vector3.one * flyoffScale;
            // Remove collider so it doesn't interfere with players.
            Destroy(proxy.GetComponent<BoxCollider>());
        }

        var rb = proxy.GetComponent<Rigidbody>() ?? proxy.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity  = true;

        // Launch away from the junction along the helix direction + random spread.
        Vector3 dir = (helixDirection + Random.insideUnitSphere * 0.4f).normalized;
        rb.AddForce(dir * flyoffForce + Vector3.up * flyoffUpForce, ForceMode.Impulse);

        Destroy(proxy, flyoffDestroyDelay);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (junctionMarker == null || overhangEndMarker == null) return;

        // Draw axis line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(junctionMarker.position, overhangEndMarker.position);

        // Draw junction sphere
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(junctionMarker.position, 0.03f);

        // Draw end sphere
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(overhangEndMarker.position, 0.02f);
    }
#endif
}
