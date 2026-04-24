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

    void Awake()
    {
        grab = GetComponentInChildren<XRGrabInteractable>(true);
        if (grab == null)
        {
            Debug.LogError($"[NetworkGrabbable] No XRGrabInteractable found on '{name}' or its children.");
            return;
        }
        grab.selectEntered.AddListener(OnGrabbed);
    }

    void OnDestroy()
    {
        if (grab != null)
            grab.selectEntered.RemoveListener(OnGrabbed);
    }

    void Update()
    {
        if (IsOwner && IsSpawned)
            Debug.Log($"[Sync] Owner {NetworkManager.Singleton.LocalClientId} moving {name} to {transform.position}");
    }

    void OnGrabbed(SelectEnterEventArgs _)
    {
        Debug.Log($"[NetworkGrabbable] Grabbed by local client {NetworkManager.Singleton.LocalClientId}, current owner {OwnerClientId}");
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
