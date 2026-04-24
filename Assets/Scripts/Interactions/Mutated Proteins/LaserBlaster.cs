using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;

// Setup requirements:
//   - This prefab needs a NetworkObject on the root.
//   - LaserBullet prefab also needs a NetworkObject (and a NetworkTransform for motion).
//   - Both prefabs must be registered in NetworkManager's Network Prefabs list.
[RequireComponent(typeof(XRGrabInteractable))]
public class LaserBlaster : NetworkBehaviour
{
    [SerializeField] public GameObject LaserBullet;
    [SerializeField] public Transform BulletSpawn;
    [SerializeField] XRGrabInteractable grab;

    [Header("Input")]
    [SerializeField] InputActionProperty scanAction;

    void Awake()
    {
        if (grab == null) grab = GetComponent<XRGrabInteractable>();
        if (grab != null) grab.selectEntered.AddListener(OnGrabbed);
    }

    public override void OnNetworkSpawn()
    {
        scanAction.action.Enable();
    }

    public override void OnNetworkDespawn()
    {
        scanAction.action.Disable();
        if (grab != null) grab.selectEntered.RemoveListener(OnGrabbed);
    }

    // Transfer ownership to the grabbing client so only that client's input fires
    // the shot and so any per-owner state stays aligned.
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

    void Update()
    {
        if (grab == null || !grab.isSelected) return;
        // Only the owner (the player currently holding it) reads input to fire.
        if (!IsOwner) return;

        if (scanAction.action.WasPressedThisFrame())
            ShootServerRpc(BulletSpawn.position, transform.rotation);
    }

    [ServerRpc(RequireOwnership = false)]
    void ShootServerRpc(Vector3 pos, Quaternion rot)
    {
        if (LaserBullet == null) return;
        var go = Instantiate(LaserBullet, pos, rot);
        var no = go.GetComponent<NetworkObject>();
        if (no != null) no.Spawn();
        else Debug.LogWarning("[LaserBlaster] LaserBullet has no NetworkObject — both players won't see it. Add one and register the prefab in NetworkManager.");
    }
}
