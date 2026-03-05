using Unity.Netcode;
using UnityEngine;

// Phase 1: Two Ku70/80 proteins orbit the DNA break site.
// P1 grabs their Ku and places it at the left DNA end; P2 places theirs at the right end.
// Placement snaps the protein in place with a glow effect. Both placed → advance to Phase 2.
// The placed Ku objects remain in the scene; Phase 7 references them for the removal animation.
public class Phase1_KuBinding : NHEJPhaseHandler
{
    [Header("Ku70/80 Pickup Prefab")]
    [Tooltip("NetworkObject + NHEJTool + ProteinOrbitController + NHEJProteinNPC")]
    [SerializeField] GameObject kuPickupPrefab;

    [Header("Placement Targets")]
    [SerializeField] Transform leftDNATarget;    // P1 places Ku here (left DNA end)
    [SerializeField] Transform rightDNATarget;   // P2 places Ku here (right DNA end)

    public override bool IsAutomatic => false;

    NetworkObject leftKuObject;
    NetworkObject rightKuObject;

    // Phase 7 reads these to animate Ku departure.
    public GameObject LeftKu  => leftKuObject  != null ? leftKuObject.gameObject  : null;
    public GameObject RightKu => rightKuObject != null ? rightKuObject.gameObject : null;
    public NetworkObject LeftKuNetworkObject  => leftKuObject;
    public NetworkObject RightKuNetworkObject => rightKuObject;

    public override void Setup() { }

    public override void StartPhase()
    {
        if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlayPhaseAdvance();

        if (!manager.IsServer || kuPickupPrefab == null) return;

        Vector3 center = GetDNACenter();
        Vector3 p1Snap = leftDNATarget  != null ? leftDNATarget.position  : manager.LeftDNAEnd.position;
        Vector3 p2Snap = rightDNATarget != null ? rightDNATarget.position : manager.RightDNAEnd.position;

        leftKuObject  = SpawnProtein(center, 1, p1Snap);
        rightKuObject = SpawnProtein(center, 2, p2Snap);
    }

    NetworkObject SpawnProtein(Vector3 spawnPos, int role, Vector3 snapPos)
    {
        var go = Instantiate(kuPickupPrefab, spawnPos, Quaternion.identity);
        var no = go.GetComponent<NetworkObject>();
        no?.Spawn();
        go.GetComponent<ProteinOrbitController>()?.Configure(role, snapPos);
        return no;
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        // Ku proteins stay in the scene after placement — they're now bound to the DNA ends.
        // Phase 7 will animate and despawn them. Clear references only.
        leftKuObject  = null;
        rightKuObject = null;
    }

    // OnProteinPlaced: base implementation calls ServerMarkPlayerComplete directly — no override needed.

    Vector3 GetDNACenter()
    {
        if (manager.LeftDNAEnd != null && manager.RightDNAEnd != null)
            return (manager.LeftDNAEnd.position + manager.RightDNAEnd.position) * 0.5f;
        return manager.LeftDNAEnd != null ? manager.LeftDNAEnd.position : Vector3.zero;
    }
}
