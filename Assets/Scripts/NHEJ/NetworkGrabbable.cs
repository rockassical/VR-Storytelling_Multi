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

    /*public override void OnNetworkSpawn()
    {
        ApplyOwnerKinematic();
    }

    protected override void OnOwnershipChanged(ulong previous, ulong current)
    {
        ApplyOwnerKinematic();
    }*/

    // Non-owners need a kinematic rigidbody so their local physics don't
    // compete with incoming NetworkTransform updates (which causes the object
    // to appear frozen on non-owner clients even though owner is broadcasting).
    /*void ApplyOwnerKinematic()
    {
        if (rb == null) return;
        rb.isKinematic = !IsOwner;
    }*/

    void OnDestroy()
    {
        if (grab != null)
            grab.selectEntered.RemoveListener(OnGrabbed);
    }

    // ── Diagnostic: owner-authoritative NetworkVariable sanity check ──────────
    // If this value syncs correctly from owner to other clients, the owner→others
    // channel works at the NGO layer and the bug is isolated to NetworkTransform.
    readonly Unity.Netcode.NetworkVariable<Vector3> _testOwnerPos = new(
        Vector3.zero,
        Unity.Netcode.NetworkVariableReadPermission.Everyone,
        Unity.Netcode.NetworkVariableWritePermission.Owner);

    float _nextLog;
    Vector3 _lastLoggedPos;
    void Update()
    {
        if (!IsSpawned) return;

        // Owner writes its live transform into the test variable every frame.
        if (IsOwner)
        {
            try { _testOwnerPos.Value = transform.position; }
            catch (System.Exception e) { /*Debug.LogError($"[NetworkGrabbable] Owner write failed: {e.Message}");*/ }
        }

        if (Time.time < _nextLog) return;
        _nextLog = Time.time + 0.5f;
        bool moved = (transform.position - _lastLoggedPos).sqrMagnitude > 0.0001f;
        _lastLoggedPos = transform.position;
        //Debug.Log($"[Sync] {name} local={NetworkManager.Singleton.LocalClientId} owner={OwnerClientId} isOwner={IsOwner} pos={transform.position} testVar={_testOwnerPos.Value} movedSinceLast={moved}");
    }

    void OnGrabbed(SelectEnterEventArgs _)
    {
        //Debug.Log($"[NetworkGrabbable] Grabbed by local client {NetworkManager.Singleton.LocalClientId}, current owner {OwnerClientId}");
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
