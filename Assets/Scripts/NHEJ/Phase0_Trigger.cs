using Unity.Netcode;
using UnityEngine;

// Phase 0: Alysia intro dialogue plays while two "ready" proteins orbit the DNA site.
// Both players grab their protein and place it at their assigned target to confirm
// they're ready. Phase advances immediately when both have placed.
public class Phase0_Trigger : NHEJPhaseHandler
{
    [Header("Dialogue")]
    [SerializeField] AudioClip alysiaIntroClip;

    [Header("Pickup Protein Prefab")]
    [Tooltip("NetworkObject + NHEJTool + ProteinOrbitController + NHEJProteinNPC")]
    [SerializeField] GameObject pickupProteinPrefab;

    [Header("Placement Targets")]
    [SerializeField] Transform player1PlacementTarget;
    [SerializeField] Transform player2PlacementTarget;

    public override bool IsAutomatic => false;

    NetworkObject protein1;
    NetworkObject protein2;

    public override void Setup() { }

    public override void StartPhase()
    {
        if (NHEJAudio.Instance != null)
        {
            NHEJAudio.Instance.PlayPhaseAdvance();
            NHEJAudio.Instance.PlayAlysiaDialogue(0);
        }

        if (!manager.IsServer || pickupProteinPrefab == null) return;

        Vector3 center = GetDNACenter();
        protein1 = SpawnPickupProtein(center, 1, player1PlacementTarget);
        protein2 = SpawnPickupProtein(center, 2, player2PlacementTarget);
    }

    NetworkObject SpawnPickupProtein(Vector3 spawnPos, int role, Transform target)
    {
        Vector3 snapPos = target != null
            ? target.position
            : spawnPos + (role == 1 ? Vector3.left : Vector3.right) * 0.4f;

        var go = Instantiate(pickupProteinPrefab, spawnPos, Quaternion.identity);
        go.GetComponent<ProteinOrbitController>()?.Configure(role, snapPos);
        var no = go.GetComponent<NetworkObject>();
        no?.Spawn();
        return no;
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        if (!manager.IsServer) return;
        if (protein1 != null && protein1.IsSpawned) protein1.Despawn();
        if (protein2 != null && protein2.IsSpawned) protein2.Despawn();
    }

    // OnProteinPlaced: base implementation calls ServerMarkPlayerComplete directly — no override needed.

    Vector3 GetDNACenter()
    {
        if (manager.LeftDNAEnd != null && manager.RightDNAEnd != null)
            return (manager.LeftDNAEnd.position + manager.RightDNAEnd.position) * 0.5f;
        if (manager.LeftDNAEnd != null) return manager.LeftDNAEnd.position;
        return Vector3.zero;
    }
}
