using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Generic orbit controller for interactive protein pickups used in Phases 0, 1, 2, 5, and 7.
// Server spawns the protein, calls Configure() to set its role and snap target AFTER Spawn()
// (values replicated via OnValueChanged). Owner drives orbit; NetworkTransform syncs to all clients.
// On release within snapRadius of the snap target, the protein locks in and reports to the
// current phase handler via NHEJManager.ReportProteinPickup().
// Does NOT require NHEJTool — ownership transfer is handled inline via RequestOwnershipServerRpc.
//
// Enemy interaction: EnemyStartCarrying() lets NHEJEnemyAI carry the protein to a bad location.
// isCorrectlyPlaced (server-computed NetworkVariable) is polled by the enemy to find targets.
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(NetworkTransform))]
public class ProteinOrbitController : NetworkBehaviour
{
    [Header("Orbit")]
    [SerializeField] float orbitRadius = 0.35f;
    [SerializeField] float orbitSpeed = 45f;    // degrees per second
    [SerializeField] float orbitHeight = 0.05f;
    [SerializeField] float snapRadius = 0.20f;

    // Configured by server after Spawn() — replicated via OnValueChanged.
    readonly NetworkVariable<int> assignedRole = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<Vector3> snapTargetPosition = new(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Continuously updated by server: true when protein is at its correct snap location.
    readonly NetworkVariable<bool> isCorrectlyPlaced = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    Vector3 orbitCenterPos;
    float currentAngle;
    bool isGrabbed;
    bool isPlaced;
    bool isEnemyCarrying;

    XRGrabInteractable grab;

    public int AssignedRole => assignedRole.Value;
    public bool IsCorrectlyPlaced => isCorrectlyPlaced.Value;
    public Vector3 SnapTargetPosition => snapTargetPosition.Value;

    /// <summary>
    /// Call on the server AFTER Spawn(). Sets the player role and world-space snap destination.
    /// Uses OnValueChanged so all clients initialize correctly when the values replicate.
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

        // Rigidbody must be kinematic — NetworkTransform owns the position.
        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        // Configure() is called AFTER Spawn(), so role arrives via OnValueChanged.
        // Handle late-join case where value is already set when we spawn.
        assignedRole.OnValueChanged += OnRoleChanged;
        if (assignedRole.Value != 0)
            OnRoleChanged(0, assignedRole.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        assignedRole.OnValueChanged -= OnRoleChanged;
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }
    }

    void OnRoleChanged(int _, int role)
    {
        if (role == 0) return;
        // Stagger starting angles so P1/P2 proteins don't overlap.
        currentAngle = role == 2 ? 180f : 0f;
        if (IsOwner) ApplyOrbitPosition();
    }

    void Update()
    {
        // Owner drives orbit (suppressed while grabbed, placed, or enemy-carried).
        if (IsOwner && !isGrabbed && !isPlaced && !isEnemyCarrying)
        {
            currentAngle = (currentAngle + orbitSpeed * Time.deltaTime) % 360f;
            ApplyOrbitPosition();
        }

        // Server continuously checks whether this protein is correctly placed.
        if (IsServer && snapTargetPosition.Value != Vector3.zero)
        {
            bool nowCorrect = !isEnemyCarrying &&
                Vector3.Distance(transform.position, snapTargetPosition.Value) <= snapRadius;

            if (nowCorrect != isCorrectlyPlaced.Value)
            {
                isCorrectlyPlaced.Value = nowCorrect;
                if (nowCorrect)
                    NHEJManager.Instance?.ReportProteinPickup(assignedRole.Value);
                else
                    NHEJManager.Instance?.ServerUnmarkPlayerComplete(assignedRole.Value);
            }
        }
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

    void OnGrabbed(SelectEnterEventArgs _)
    {
        isGrabbed = true;
        // Transfer ownership to the grabbing client so they drive position locally.
        RequestOwnershipServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestOwnershipServerRpc(ServerRpcParams rpcParams = default)
    {
        NetworkObject.ChangeOwnership(rpcParams.Receive.SenderClientId);
    }

    void OnReleased(SelectExitEventArgs _)
    {
        isGrabbed = false;
        if (isPlaced) return;

        if (Vector3.Distance(transform.position, snapTargetPosition.Value) < snapRadius)
        {
            isPlaced = true;
            PlaceProteinServerRpc();
        }
        // Not close enough → orbit resumes in Update().
    }

    [ServerRpc(RequireOwnership = false)]
    void PlaceProteinServerRpc()
    {
        // Snap visuals and local effects on all clients.
        // Completion logic is handled by the server Update's isCorrectlyPlaced check.
        ConfirmPlacementClientRpc(snapTargetPosition.Value);
    }

    [ClientRpc]
    void ConfirmPlacementClientRpc(Vector3 snapPos)
    {
        isPlaced = true;
        transform.position = snapPos;

        var npc = GetComponent<NHEJProteinNPC>();
        if (npc != null) npc.SetGlow(true);

        if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlaySnap();

        // Let the current phase handler react locally on every client.
        NHEJManager.Instance?.GetCurrentPhaseHandler()?.OnProteinPlacedLocal(assignedRole.Value);
    }

    // ── Enemy interaction ─────────────────────────────────────────────────────

    /// <summary>
    /// Called server-side by NHEJEnemyAI when it begins carrying this protein.
    /// Takes ownership to server and resets placement state on all clients.
    /// </summary>
    public void EnemyStartCarrying()
    {
        if (!IsServer) return;
        isEnemyCarrying = true;
        NetworkObject.ChangeOwnership(NetworkManager.ServerClientId);
        ResetPlacementClientRpc();
    }

    /// <summary>
    /// Called server-side by NHEJEnemyAI when it drops the protein at a bad location.
    /// Releases carry state so the protein resumes orbiting.
    /// </summary>
    public void EnemyRelease(Vector3 dropPos)
    {
        if (!IsServer) return;
        isEnemyCarrying = false;
        transform.position = dropPos;
        // isCorrectlyPlaced will auto-update next frame based on distance.
    }

    /// <summary>Resets placement state on all clients (called when enemy steals or re-grab needed).</summary>
    [ClientRpc]
    void ResetPlacementClientRpc()
    {
        isPlaced = false;
        isGrabbed = false;
        // isEnemyCarrying is server-only state — do NOT reset it here.
        var npc = GetComponent<NHEJProteinNPC>();
        if (npc != null) npc.SetGlow(false);
    }
}
