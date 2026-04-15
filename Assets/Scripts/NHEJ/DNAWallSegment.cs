using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A grabbable DNA wall segment (or nucleotide block) used in Phase 4 gap fill.
//
// Designers place these in the scene at their "broken" positions.
// Each segment has a correctPlacementMarker child Transform that marks the ideal reassembly position.
// Players grab and reposition the segment, then seal it with LigaseSprayCan.
// At seal time, distance from the correct position determines the placement score (0–1).
//
// Scene NetworkObject: place this prefab in the scene directly — no runtime spawn needed.
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(XRGrabInteractable))]
public class DNAWallSegment : NetworkBehaviour
{
    [Header("Correct Placement")]
    [Tooltip("Child Transform that marks the ideal reassembly position/rotation.")]
    [SerializeField] Transform correctPlacementMarker;
    [Tooltip("Distance at which placement score reaches 0. At 0 distance score = 1.")]
    [SerializeField] float maxScoringDistance = 0.25f;

    [Header("Visual Feedback")]
    [SerializeField] Renderer segmentRenderer;
    [SerializeField] Color defaultColor = Color.white;
    [SerializeField] Color nearCorrectColor = new Color(1f, 0.85f, 0.1f);   // yellow hint
    [SerializeField] Color sealedColor    = new Color(0.25f, 1f, 0.45f);    // green
    [SerializeField] GameObject sealVFX;

    readonly NetworkVariable<bool> netIsSealed = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Set at runtime by Phase4_GapFill when segments are spawned dynamically.
    // Takes precedence over correctPlacementMarker when non-zero.
    readonly NetworkVariable<Vector3> netCorrectPosition = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsSealed => netIsSealed.Value;
    public float PlacementScore { get; private set; }

    /// <summary>World-space position the segment should end up at.</summary>
    public Vector3 CorrectPosition
    {
        get
        {
            if (netCorrectPosition.Value != Vector3.zero) return netCorrectPosition.Value;
            return correctPlacementMarker != null ? correctPlacementMarker.position : transform.position;
        }
    }

    /// <summary>Server-only. Sets the target reassembly position for this segment.</summary>
    public void SetCorrectPosition(Vector3 worldPos)
    {
        if (IsServer) netCorrectPosition.Value = worldPos;
    }

    XRGrabInteractable grab;
    Rigidbody rb;
    bool isHeld;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        rb   = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();

        netIsSealed.OnValueChanged += OnSealedChanged;

        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        ApplyColor(defaultColor);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        netIsSealed.OnValueChanged -= OnSealedChanged;
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }
    }

    // ── Grab events ───────────────────────────────────────────────────────────

    void OnGrabbed(SelectEnterEventArgs _)
    {
        isHeld = true;
        if (!netIsSealed.Value)
            RequestOwnershipServerRpc();
    }

    void OnReleased(SelectExitEventArgs _) => isHeld = false;

    [ServerRpc(RequireOwnership = false)]
    void RequestOwnershipServerRpc(ServerRpcParams p = default)
    {
        if (!netIsSealed.Value)
            NetworkObject.ChangeOwnership(p.Receive.SenderClientId);
    }

    // ── Proximity color hint (owner-driven) ───────────────────────────────────

    void Update()
    {
        if (netIsSealed.Value || !IsOwner || isHeld || segmentRenderer == null
            || correctPlacementMarker == null) return;

        float dist = Vector3.Distance(transform.position, correctPlacementMarker.position);
        float t = 1f - Mathf.Clamp01(dist / maxScoringDistance);
        ApplyColor(Color.Lerp(defaultColor, nearCorrectColor, t * t));
    }

    // ── Sealing (called server-side by LigaseSprayCan) ────────────────────────

    /// <summary>Server-only. Seals this segment and records a placement score.</summary>
    public void Seal(float score)
    {
        if (!IsServer || netIsSealed.Value) return;
        PlacementScore  = score;
        netIsSealed.Value = true;
        SealClientRpc(score);
    }

    [ClientRpc]
    void SealClientRpc(float score)
    {
        PlacementScore = score;

        // Freeze physics
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        if (grab != null) grab.enabled = false;

        ApplyColor(sealedColor);
        if (sealVFX != null) sealVFX.SetActive(true);
        if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlaySnap();
    }

    void OnSealedChanged(bool _, bool now)
    {
        if (now) ApplyColor(sealedColor);
    }

    void ApplyColor(Color c)
    {
        if (segmentRenderer != null)
            segmentRenderer.material.color = c;
    }

    // ── Editor gizmo ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (correctPlacementMarker == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(correctPlacementMarker.position, 0.05f);
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawSphere(correctPlacementMarker.position, maxScoringDistance);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, correctPlacementMarker.position);
    }
#endif
}
