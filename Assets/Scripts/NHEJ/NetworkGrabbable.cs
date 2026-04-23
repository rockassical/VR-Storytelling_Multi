using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Add to any spawned NetworkObject that players can grab.
// Transfers NGO ownership to the grabbing client so NetworkTransform
// replicates the movement to all other players.
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(XRGrabInteractable))]
public class NetworkGrabbable : NetworkBehaviour
{
    XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    void OnDestroy()
    {
        if (grab == null) return;
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    void OnGrabbed(SelectEnterEventArgs _)
    {
        if (!IsSpawned) return;
        ulong localId = NetworkManager.Singleton.LocalClientId;
        if (OwnerClientId != localId)
            RequestOwnershipServerRpc(localId);
    }

    void OnReleased(SelectExitEventArgs _) { }

    [ServerRpc(RequireOwnership = false)]
    void RequestOwnershipServerRpc(ulong requesterId)
    {
        NetworkObject.ChangeOwnership(requesterId);
    }
}
