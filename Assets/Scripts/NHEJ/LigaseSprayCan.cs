using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRMultiplayer;

[RequireComponent(typeof(XRGrabInteractable))]
public class LigaseSprayCan : NetworkBaseInteractable
{
    [Header("Spray Settings")]
    [SerializeField] float sprayRange = 2f;
    [SerializeField] GameObject sprayVFXPrefab;
    [SerializeField] float sprayCooldown = 0.5f;

    [Header("Return-to-Home")]
    [SerializeField] float returnMoveSpeed   = 1.5f;
    [SerializeField] float returnRotateSpeed = 120f;

    [Header("Input")]
    [SerializeField] InputActionProperty sprayAction;

    [Header("Debug")]
    [SerializeField] bool debugMode = false;
    [SerializeField] Key debugSprayKey = Key.Space;

    Vector3    homePosition;
    Quaternion homeRotation;
    bool isSpraying;
    float cooldownTimer;
    GameObject activeVFX;

    public bool firstSeal;
    public GameManager gameManager;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    // NOTE: Do NOT define OnEnable/OnDisable here — NetworkBaseInteractable.OnEnable
    // is private and Unity would call ours instead, breaking SetupListeners.
    // Enable the action in OnNetworkSpawn instead.

    void Start()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;

        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        sprayAction.action.Enable();

        firstSeal = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        sprayAction.action.Enable();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        sprayAction.action.Disable();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        cooldownTimer -= Time.deltaTime;

        // VR trigger
        if (sprayAction.action.WasPressedThisFrame())  StartSpraying();
        if (sprayAction.action.WasReleasedThisFrame()) StopSpraying();

        // Debug keyboard
        if (debugMode && Keyboard.current != null)
        {
            if (Keyboard.current[debugSprayKey].wasPressedThisFrame)  StartSpraying();
            if (Keyboard.current[debugSprayKey].wasReleasedThisFrame) StopSpraying();
        }

        // Seal logic on cooldown while spraying
        if (isSpraying && cooldownTimer <= 0f)
        {
            cooldownTimer = sprayCooldown;
            Vector3 origin = transform.position + Vector3.up * 0.2f;

            DNASealPoint sealPoint = FindNearestPendingSealPoint(origin);
            if (sealPoint != null)
            {
                sealPoint.Seal();
                CheckGapBridged();

                if(firstSeal){
                    firstSeal = false;
                    gameManager.playPhase(3);
                }
            }
        }

        // Return to home when not held
        if (!IsOwner || m_BaseInteractable.isSelected) return;

        transform.position = Vector3.MoveTowards(
            transform.position, homePosition, returnMoveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, homeRotation, returnRotateSpeed * Time.deltaTime);
    }

    // ── Spray ─────────────────────────────────────────────────────────────────

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

        if (activeVFX != null) { Destroy(activeVFX); activeVFX = null; }
    }

    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, 200, 180, 60));
        GUILayout.Label($"Hold [{debugSprayKey}] to spray");
        GUILayout.Label($"Spraying: {isSpraying} | Range: {sprayRange}m");
        GUILayout.EndArea();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void CheckGapBridged()
    {
        // BFS runs on the spraying client — it holds the complete in-memory graph
        // (DNASealPoint.leftSealedWall / rightSealedWall set by Seal() locally).
        // The server never has those refs, so we check here and only notify the server
        // when the gap is actually bridged.
        var bp = NHEJManager.Instance?.BreakPoint;
        if (bp != null && bp.IsGapBridged())
            NHEJManager.Instance.NotifyGapBridgedServerRpc();
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
