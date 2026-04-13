using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRMultiplayer;

// LigaseIV as a spray can used in Phase 4 gap fill.
//
// Player grabs it (NetworkBaseInteractable transfers ownership) and pulls the trigger
// (XRI Activated event) to spray/seal the nearest unsealed DNAWallSegment within sprayRange.
// When released it drifts back to its spawn position.
//
// Must be registered in NetworkManager's NetworkPrefabs list.
// Spawned at runtime by Phase4_GapFill.
[RequireComponent(typeof(XRGrabInteractable))]
public class LigaseSprayCan : NetworkBaseInteractable
{
    [Header("Spray Settings")]
    [SerializeField] float sprayRange = 0.5f;
    [SerializeField] GameObject sprayVFXPrefab;
    [SerializeField] float sprayCooldown = 0.5f;

    [Header("Return-to-Home")]
    [SerializeField] float returnMoveSpeed   = 1.5f;
    [SerializeField] float returnRotateSpeed = 120f;   // degrees per second

    [Header("Input")]
    [SerializeField] InputActionProperty sprayAction;

    [Header("Debug")]
    [SerializeField] bool debugMode = false;
    [Tooltip("Press this key to trigger a spray at the can's current position (debug only).")]
    [SerializeField] Key debugSprayKey = Key.Space;

    Vector3    homePosition;
    Quaternion homeRotation;
    bool isSpraying;
    float cooldownTimer;
    GameObject activeVFX;
    XRGrabInteractable grab;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        homePosition = transform.position;
        homeRotation = transform.rotation;

        grab = GetComponent<XRGrabInteractable>();

        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
    }

    void OnEnable()  => sprayAction.action.Enable();
    void OnDisable() => sprayAction.action.Disable();

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        cooldownTimer -= Time.deltaTime;

        // VR trigger
        bool isSelected = grab != null && grab.isSelected;
        if (isSelected)
        {
            if (sprayAction.action.WasPressedThisFrame())   StartSpraying();
            if (sprayAction.action.WasReleasedThisFrame())  StopSpraying();
        }
        else if (!debugMode && isSpraying)
        {
            StopSpraying();
        }

        // Debug keyboard
        if (debugMode && Keyboard.current != null)
        {
            if (Keyboard.current[debugSprayKey].wasPressedThisFrame)  StartSpraying();
            if (Keyboard.current[debugSprayKey].wasReleasedThisFrame) StopSpraying();
        }

        // Seal logic runs on cooldown while spraying
        if (isSpraying && cooldownTimer <= 0f)
        {
            cooldownTimer = sprayCooldown;
            Vector3 origin = transform.position + Vector3.up * 0.2f;

            DNAWallSegment nearest = FindNearestUnsealedSegment(origin);
            if (nearest != null)
                RequestSealServerRpc(nearest.NetworkObjectId, origin);

            DNASealPoint sealPoint = FindNearestPendingSealPoint(origin);
            if (sealPoint != null)
                sealPoint.Seal();
        }

        if (!IsOwner || isSelected) return;

        transform.position = Vector3.MoveTowards(
            transform.position, homePosition, returnMoveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, homeRotation, returnRotateSpeed * Time.deltaTime);
    }

    void StartSpraying()
    {
        if (isSpraying) return;
        isSpraying = true;

        if (sprayVFXPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.2f;
            activeVFX = Instantiate(sprayVFXPrefab, spawnPos, transform.rotation, transform);
        }

        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayLigationSuccess();
    }

    void StopSpraying()
    {
        if (!isSpraying) return;
        isSpraying = false;

        if (activeVFX != null)
        {
            Destroy(activeVFX);
            activeVFX = null;
        }
    }

    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, 200, 180, 60));
        GUILayout.Label($"Hold [{debugSprayKey}] to spray");
        GUILayout.Label($"Spraying: {isSpraying} | Range: {sprayRange}m");
        GUILayout.EndArea();
    }

    // ── ServerRpc ─────────────────────────────────────────────────────────────

    [ServerRpc]
    void RequestSealServerRpc(ulong segmentNetId, Vector3 fromPosition)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(segmentNetId, out var no)) return;

        var seg = no.GetComponent<DNAWallSegment>();
        if (seg == null || seg.IsSealed) return;

        // Server-side range re-check (guards against stale client position)
        float dist = Vector3.Distance(fromPosition, seg.transform.position);
        if (dist > sprayRange * 1.5f) return;

        // Placement score: how close to the correct position
        float placementDist = Vector3.Distance(seg.transform.position, seg.CorrectPosition);
        float score = 1f - Mathf.Clamp01(placementDist / 0.25f);

        seg.Seal(score);

        // Notify Phase4 handler
        var phase4 = NHEJManager.Instance?.GetCurrentPhaseHandler() as Phase4_GapFill;
        phase4?.OnSegmentSealed(seg, score);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    DNAWallSegment FindNearestUnsealedSegment(Vector3 origin)
    {
        var all = FindObjectsOfType<DNAWallSegment>();
        DNAWallSegment nearest = null;
        float nearestDist = sprayRange;

        foreach (var seg in all)
        {
            if (seg.IsSealed) continue;
            float d = Vector3.Distance(origin, seg.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = seg; }
        }
        return nearest;
    }

    DNASealPoint FindNearestPendingSealPoint(Vector3 origin)
    {
        var all = FindObjectsOfType<DNASealPoint>();
        DNASealPoint nearest = null;
        float nearestDist = sprayRange;

        foreach (var sp in all)
        {
            if (!sp.HasPending) continue;
            float d = Vector3.Distance(origin, sp.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = sp; }
        }
        return nearest;
    }

}
