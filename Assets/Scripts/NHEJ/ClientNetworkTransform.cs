using Unity.Netcode.Components;
using UnityEngine;

// Owner-authoritative NetworkTransform: whichever client currently owns the
// NetworkObject drives position/rotation. Combine with NetworkGrabbable so
// ownership transfers on grab — then the grabbing player moves the object
// and everyone else sees it smoothly (no server-snap-back).
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"[ClientNT] Spawn {name} IsServer={IsServer} IsOwner={IsOwner} CanCommitToTransform={CanCommitToTransform} ServerAuthoritative={IsServerAuthoritative()}");
    }

    protected override void OnOwnershipChanged(ulong previous, ulong current)
    {
        base.OnOwnershipChanged(previous, current);
        Debug.Log($"[ClientNT] OwnershipChanged {name} {previous}->{current} local={NetworkManager.Singleton.LocalClientId} IsOwner={IsOwner} CanCommitToTransform={CanCommitToTransform}");
    }
}
