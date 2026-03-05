using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Generic orbit controller for interactive protein pickups used in Phases 0, 1, 2, 5, and 7.
// Server spawns the protein, calls Configure() to set its role and snap target BEFORE Spawn()
// (values ride in the spawn snapshot). Owner drives orbit; NetworkTransform syncs to all clients.
// On release within snapRadius of the snap target, the protein locks in and reports to the
// current phase handler via NHEJManager.ReportProteinPickup().
[RequireComponent(typeof(NHEJTool))]
public class ProteinOrbitController : NetworkBehaviour
{
    [Header("Orbit")]
    [SerializeField] float orbitRadius = 0.35f;
    [SerializeField] float orbitSpeed = 45f;    // degrees per second
    [SerializeField] float orbitHeight = 0.05f;
    [SerializeField] float snapRadius = 0.20f;

    // Configured by server before Spawn() — included in spawn snapshot.
    readonly NetworkVariable<int> assignedRole = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<Vector3> snapTargetPosition = new(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    Vector3 orbitCenterPos;
    float currentAngle;
    bool isGrabbed;
    bool isPlaced;

    XRGrabInteractable grab;

    public int AssignedRole => assignedRole.Value;

    /// <summary>
    /// Call on the server BEFORE Spawn(). Sets the player role and world-space snap destination.
    /// P1 orbits start at 0° and P2 at 180° so they don't overlap.
    /// </summary>
    public void Configure(int role, Vector3 snapPos)
    {
        assignedRole.Value = role;
        snapTargetPosition.Value = snapPos;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (NHEJManager.Instance?.LeftDNAEnd != null && NHEJManager.Instance?.RightDNAEnd != null)
            orbitCenterPos = (NHEJManager.Instance.LeftDNAEnd.position
                            + NHEJManager.Instance.RightDNAEnd.position) * 0.5f;

        // Stagger starting angles so P1/P2 proteins don't spawn on top of each other.
        currentAngle = assignedRole.Value == 2 ? 180f : 0f;

        grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        if (IsOwner) ApplyOrbitPosition();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }
    }

    void Update()
    {
        if (!IsOwner || isGrabbed || isPlaced) return;
        currentAngle = (currentAngle + orbitSpeed * Time.deltaTime) % 360f;
        ApplyOrbitPosition();
    }

    void ApplyOrbitPosition()
    {
        float rad = currentAngle * Mathf.Deg2Rad;
        transform.position = orbitCenterPos + new Vector3(
            Mathf.Cos(rad) * orbitRadius,
            orbitHeight,
            Mathf.Sin(rad) * orbitRadius);
        transform.LookAt(orbitCenterPos);
    }

    void OnGrabbed(SelectEnterEventArgs _) => isGrabbed = true;

    void OnReleased(SelectExitEventArgs _)
    {
        isGrabbed = false;
        if (isPlaced) return;

        if (Vector3.Distance(transform.position, snapTargetPosition.Value) < snapRadius)
        {
            isPlaced = true;
            PlaceProteinServerRpc(NetworkManager.Singleton.LocalClientId);
        }
        // Not close enough → orbit resumes in Update().
    }

    [ServerRpc(RequireOwnership = false)]
    void PlaceProteinServerRpc(ulong clientId)
    {
        ConfirmPlacementClientRpc(snapTargetPosition.Value);
        NHEJManager.Instance?.ReportProteinPickup(assignedRole.Value);
    }

    [ClientRpc]
    void ConfirmPlacementClientRpc(Vector3 snapPos)
    {
        isPlaced = true;
        transform.position = snapPos;

        var npc = GetComponent<NHEJProteinNPC>();
        if (npc != null) npc.SetGlow(true);

        if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlaySnap();

        // Let the current phase handler react locally on every client
        // (e.g. Phase7 starts Ku departure animation, Phase5 starts alignment).
        NHEJManager.Instance?.GetCurrentPhaseHandler()?.OnProteinPlacedLocal(assignedRole.Value);
    }
}
