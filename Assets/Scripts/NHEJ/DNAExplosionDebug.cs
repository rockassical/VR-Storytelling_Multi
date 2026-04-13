using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Debug tool for testing the explosion/overhang system without running the full game flow.
// Attach to any GameObject in the scene (e.g. the NHEJManager or DNA root).
//
// Lists the specific polySurface objects that form the test explosion area.
// On trigger, explodes them with randomised physics and restores them after a delay.
public class DNAExplosionDebug : MonoBehaviour
{
    [Header("Target Surfaces")]
    [Tooltip("Names of child objects to explode. Searched across all GameObjects in the scene.")]
    [SerializeField] List<string> targetNames = new List<string>
    {
        "polySurface1139",  "polySurface1139.001",
        "polySurface1149",  "polySurface1149.001", "polySurface1149.002",
        "polySurface1250.001", "polySurface1250.004", "polySurface1250.005",
        "polySurface1274.001", "polySurface1274.023",
        "polySurface998",   "polySurface998.001"
    };

    [Header("Explosion Settings")]
    [SerializeField] float explosionForce    = 2f;
    [SerializeField] float explosionUpBias   = 0.8f;
    [SerializeField] float restoreDelay      = 3f;   // seconds before surfaces snap back

    [Header("Debug")]
    [SerializeField] bool showDebugUI = true;

    bool exploding;

    // ── GUI ───────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!showDebugUI) return;

        GUILayout.BeginArea(new Rect(10, 270, 200, 60));
        GUI.enabled = !exploding;
        if (GUILayout.Button("Test DNA Explosion"))
            StartCoroutine(ExplodeAndRestore());
        GUI.enabled = true;
        if (exploding)
            GUILayout.Label($"Restoring in {restoreDelay:F1}s...");
        GUILayout.EndArea();
    }

    // ── Explosion ─────────────────────────────────────────────────────────────

    IEnumerator ExplodeAndRestore()
    {
        exploding = true;

        int seed = UnityEngine.Random.Range(0, int.MaxValue);
        var rng  = new System.Random(seed);
        Debug.Log($"[DNAExplosionDebug] Exploding with seed={seed}");

        // Find targets and store their original state
        var targets = new List<(GameObject go, Transform parent, Vector3 localPos, Quaternion localRot)>();

        foreach (string name in targetNames)
        {
            var go = FindByName(name);
            if (go == null) { Debug.LogWarning($"[DNAExplosionDebug] Could not find '{name}'"); continue; }
            targets.Add((go, go.transform.parent, go.transform.localPosition, go.transform.localRotation));
        }

        // Explode
        foreach (var (go, _, _, _) in targets)
        {
            go.transform.SetParent(null);

            var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity  = true;

            Vector3 outward   = (go.transform.position - transform.position).normalized;
            Vector3 direction = (outward + Vector3.up * explosionUpBias +
                                 new Vector3((float)(rng.NextDouble() - 0.5),
                                             (float)(rng.NextDouble() - 0.5),
                                             (float)(rng.NextDouble() - 0.5)) * 0.3f).normalized;
            rb.AddForce(direction * explosionForce, ForceMode.Impulse);
            rb.AddTorque(new Vector3((float)(rng.NextDouble() - 0.5f),
                                     (float)(rng.NextDouble() - 0.5f),
                                     (float)(rng.NextDouble() - 0.5f)) * 2f, ForceMode.Impulse);
        }

        yield return new WaitForSeconds(restoreDelay);

        // Restore
        foreach (var (go, parent, localPos, localRot) in targets)
        {
            if (go == null) continue;

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity        = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;

            if (rb != null) Destroy(rb);
        }

        exploding = false;
        Debug.Log("[DNAExplosionDebug] Surfaces restored.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject FindByName(string name)
    {
        foreach (var go in FindObjectsOfType<GameObject>(true))
            if (go.name == name) return go;
        return null;
    }
}
