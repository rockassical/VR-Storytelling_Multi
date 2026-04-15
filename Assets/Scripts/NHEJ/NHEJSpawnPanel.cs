using Unity.Netcode;
using UnityEngine;

// World-space UI panel with three spawn buttons: Nucleotide, Wall, LigaseIV Spray Can.
// Players point their VR ray at a button and click to request a networked spawn.
//
// Scene setup:
//   1. Create a Canvas:  Render Mode = World Space, scale it down (~0.002 x 0.002).
//   2. Add component  TrackedDeviceGraphicRaycaster  (from XRI package) to the Canvas.
//   3. Make sure the scene EventSystem has an  XRUIInputModule  (replaces Standalone Input Module).
//   4. Add three UI Buttons as children of the Canvas.
//   5. Attach this script to the Canvas (or any NetworkObject in the scene).
//   6. Wire each button's OnClick → NHEJSpawnPanel → SpawnNucleotide / SpawnWall / SpawnSprayCan.
//   7. Assign the three prefabs and (optionally) a spawnPoint transform in the inspector.
//      If spawnPoint is left empty, objects spawn in front of the panel.
//
// Prefab requirements:
//   - Each prefab needs a NetworkObject component.
//   - All three must be registered in NetworkManager → Network Prefabs.
public class NHEJSpawnPanel : NetworkBehaviour
{
    [Header("Spawn Prefabs")]
    [Tooltip("Nucleotide prefab (NetworkObject required).")]
    [SerializeField] GameObject nucleotidePrefab;

    [Tooltip("DNAWallSegment prefab (NetworkObject required).")]
    [SerializeField] GameObject wallPrefab;

    [Tooltip("LigaseIV spray can prefab (NetworkObject required).")]
    [SerializeField] GameObject sprayCanPrefab;

    [Header("Spawn Location")]
    [Tooltip("Where spawned objects appear. Leave empty to spawn in front of this panel.")]
    [SerializeField] Transform spawnPoint;

    [Tooltip("Each successive spawn is offset by this much so items don't stack.")]
    [SerializeField] float spawnSpread = 0.12f;

    int spawnIndex;

    // ── Button callbacks (wire these in the Inspector OnClick events) ─────────

    public void SpawnNucleotide()
    {
        if (!CheckCanSpawn(nucleotidePrefab, "nucleotide")) return;
        RequestSpawnServerRpc(SpawnType.Nucleotide);
    }

    public void SpawnWall()
    {
        if (!CheckCanSpawn(wallPrefab, "wall")) return;
        RequestSpawnServerRpc(SpawnType.Wall);
    }

    public void SpawnSprayCan()
    {
        if (!CheckCanSpawn(sprayCanPrefab, "spray can")) return;
        RequestSpawnServerRpc(SpawnType.SprayCan);
    }

    // ── Network ───────────────────────────────────────────────────────────────

    enum SpawnType { Nucleotide, Wall, SprayCan }

    [ServerRpc(RequireOwnership = false)]
    void RequestSpawnServerRpc(SpawnType type)
    {
        GameObject prefab = type switch
        {
            SpawnType.Nucleotide => nucleotidePrefab,
            SpawnType.Wall       => wallPrefab,
            SpawnType.SprayCan   => sprayCanPrefab,
            _                    => null
        };

        if (prefab == null) return;

        Vector3 pos = GetSpawnPosition();
        var go = Instantiate(prefab, pos, Quaternion.identity);
        var no = go.GetComponent<NetworkObject>();
        no?.Spawn();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    Vector3 GetSpawnPosition()
    {
        Vector3 origin = spawnPoint != null
            ? spawnPoint.position
            : transform.position + transform.forward * 0.3f + Vector3.down * 0.1f;

        // Spread successive spawns so they don't land on top of each other.
        Vector3 offset = transform.right * ((spawnIndex % 5 - 2) * spawnSpread)
                       + Vector3.up      * (spawnIndex / 5       * spawnSpread);
        spawnIndex++;

        return origin + offset;
    }

    bool CheckCanSpawn(GameObject prefab, string label)
    {
        if (prefab != null) return true;
        Debug.LogWarning($"[NHEJSpawnPanel] {label} prefab not assigned.");
        return false;
    }
}
