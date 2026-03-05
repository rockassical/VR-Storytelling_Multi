using Unity.Netcode;
using UnityEngine;

// Phase 2: Two DNA-PKcs proteins orbit the DNA break site.
// P1 grabs theirs and places it at the left Ku dock point; P2 docks the right side.
// Placement triggers a glow (autophosphorylation cue). Both placed → advance to Phase 3.
// The placed PKcs objects stay in the scene; Phase 5 references them for the departure animation.
public class Phase2_DNAPKcs : NHEJPhaseHandler
{
    [Header("DNA-PKcs Pickup Prefab")]
    [Tooltip("NetworkObject + NHEJTool + ProteinOrbitController + NHEJProteinNPC")]
    [SerializeField] GameObject dnaPKcsPickupPrefab;

    [Header("Placement Targets")]
    [SerializeField] Transform leftDockTarget;    // P1 docks here (on top of left Ku)
    [SerializeField] Transform rightDockTarget;   // P2 docks here (on top of right Ku)

    public override bool IsAutomatic => false;

    NetworkObject leftPKcsObject;
    NetworkObject rightPKcsObject;

    // Phase 5 reads these to animate PKcs departure.
    public GameObject LeftPKcs  => leftPKcsObject  != null ? leftPKcsObject.gameObject  : null;
    public GameObject RightPKcs => rightPKcsObject != null ? rightPKcsObject.gameObject : null;
    public NetworkObject LeftPKcsNetworkObject  => leftPKcsObject;
    public NetworkObject RightPKcsNetworkObject => rightPKcsObject;

    public override void Setup() { }

    public override void StartPhase()
    {
        if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlayPhaseAdvance();

        if (!manager.IsServer || dnaPKcsPickupPrefab == null) return;

        Vector3 center = GetDNACenter();
        Vector3 p1Snap = leftDockTarget  != null ? leftDockTarget.position  : manager.LeftDNAEnd.position  + Vector3.up * 0.3f;
        Vector3 p2Snap = rightDockTarget != null ? rightDockTarget.position : manager.RightDNAEnd.position + Vector3.up * 0.3f;

        leftPKcsObject  = SpawnProtein(center, 1, p1Snap);
        rightPKcsObject = SpawnProtein(center, 2, p2Snap);
    }

    NetworkObject SpawnProtein(Vector3 spawnPos, int role, Vector3 snapPos)
    {
        var go = Instantiate(dnaPKcsPickupPrefab, spawnPos, Quaternion.identity);
        go.GetComponent<ProteinOrbitController>()?.Configure(role, snapPos);
        var no = go.GetComponent<NetworkObject>();
        no?.Spawn();
        return no;
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        // PKcs proteins stay in the scene after placement.
        // Phase 5 will animate their departure and despawn them.
        leftPKcsObject  = null;
        rightPKcsObject = null;
    }

    // OnProteinPlacedLocal: trigger autophosphorylation pulse on the placed protein.
    public override void OnProteinPlacedLocal(int playerRole)
    {
        var go = playerRole == 1 ? LeftPKcs : RightPKcs;
        if (go == null) return;

        var npc = go.GetComponent<NHEJProteinNPC>();
        if (npc != null) StartCoroutine(npc.PulseGlow(2.5f));
    }

    // OnProteinPlaced: base implementation calls ServerMarkPlayerComplete directly — no override needed.

    Vector3 GetDNACenter()
    {
        if (manager.LeftDNAEnd != null && manager.RightDNAEnd != null)
            return (manager.LeftDNAEnd.position + manager.RightDNAEnd.position) * 0.5f;
        return manager.LeftDNAEnd != null ? manager.LeftDNAEnd.position : Vector3.zero;
    }
}
