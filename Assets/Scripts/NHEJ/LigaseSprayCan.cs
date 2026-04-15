using Unity.Netcode;
using UnityEngine;
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
    [Tooltip("Transform at the tip of the can — spray VFX spawns here. Falls back to centre if null.")]
    [SerializeField] Transform tipTransform;
    [SerializeField] GameObject sprayVFXPrefab;
    [SerializeField] float sprayCooldown = 0.5f;

    [Header("Return-to-Home")]
    [SerializeField] float returnMoveSpeed   = 1.5f;
    [SerializeField] float returnRotateSpeed = 120f;   // degrees per second

    Vector3    homePosition;
    Quaternion homeRotation;
    bool isHeld;
    float cooldownTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        homePosition = transform.position;
        homeRotation = transform.rotation;

        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }
    }

    // ── Grab events ───────────────────────────────────────────────────────────

    void OnGrabbed(SelectEnterEventArgs _) => isHeld = true;
    void OnReleased(SelectExitEventArgs _) => isHeld = false;

    // ── Update: drift back to home ────────────────────────────────────────────

    void Update()
    {
        cooldownTimer -= Time.deltaTime;
        if (!IsOwner || isHeld) return;

        transform.position = Vector3.MoveTowards(
            transform.position, homePosition, returnMoveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, homeRotation, returnRotateSpeed * Time.deltaTime);
    }

    // ── Trigger / Activated ───────────────────────────────────────────────────

    // Unlike NHEJTool (which keeps Activated as a no-op for placement-based tools),
    // the spray can is explicitly trigger-activated — that IS the mechanic.
    public override void Activated(bool activate)
    {
        base.Activated(activate);

        if (!activate || !isHeld || cooldownTimer > 0f) return;
        cooldownTimer = sprayCooldown;

        Vector3 origin = tipTransform != null ? tipTransform.position : transform.position;

        DNAWallSegment nearest = FindNearestUnsealedSegment(origin);
        if (nearest == null) return;

        // Local VFX / audio
        SpawnSprayVFX(origin);

        // Server validates and seals
        RequestSealServerRpc(nearest.NetworkObjectId, origin);
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

    void SpawnSprayVFX(Vector3 pos)
    {
        if (sprayVFXPrefab != null)
        {
            var vfx = Instantiate(sprayVFXPrefab, pos, transform.rotation);
            Destroy(vfx, 2f);
        }
        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayLigationSuccess();
    }
}
