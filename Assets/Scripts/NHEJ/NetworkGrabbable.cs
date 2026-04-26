using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Add to any spawned NetworkObject that players can grab.
// Transfers NGO ownership to the grabbing client so NetworkTransform
// (use ClientNetworkTransform for smooth motion) replicates movement to all other players.
//
// The XRGrabInteractable may be on the root OR on a child — this script finds whichever.
[RequireComponent(typeof(NetworkObject))]
public class NetworkGrabbable : NetworkBehaviour
{
    XRGrabInteractable grab;
    Rigidbody rb;

    void Awake()
    {
        grab = GetComponentInChildren<XRGrabInteractable>(true);
        if (grab == null)
        {
            //Debug.LogError($"[NetworkGrabbable] No XRGrabInteractable found on '{name}' or its children.");
            return;
        }
        grab.selectEntered.AddListener(OnGrabbed);
        rb = GetComponentInChildren<Rigidbody>(true);
    }

    public override void OnNetworkSpawn()
    {
        // Local-only parent changes (grab/socket); world transform syncs via OwnerTransformSync.
        if (NetworkObject != null) NetworkObject.AutoObjectParentSync = false;
        //ApplyOwnerKinematic();
    }

    protected override void OnOwnershipChanged(ulong previous, ulong current)
    {
        //ApplyOwnerKinematic();
    }

    //void ApplyOwnerKinematic()
    //{
    //    if (rb == null) return;
    //    rb.isKinematic = !IsOwner;
    //}

    void OnDestroy()
    {
        if (grab != null)
            grab.selectEntered.RemoveListener(OnGrabbed);
    }

    void OnGrabbed(SelectEnterEventArgs _)
    {
        if (!IsSpawned) return;
        ulong localId = NetworkManager.Singleton.LocalClientId;
        if (OwnerClientId != localId)
            RequestOwnershipServerRpc(localId);
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestOwnershipServerRpc(ulong requesterId)
    {
        NetworkObject.ChangeOwnership(requesterId);
    }
}
