using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Enemy AI for NHEJ phases. Spawned by NHEJManager at the start of each player-interactive phase.
// Orbits the DNA break site during idle, then seeks a randomly chosen correctly-placed protein,
// carries it ~stealDropRadius metres away to displace it, then loops.
//
// Players can grab this enemy (XRGrabInteractable). If released more than stunDistance metres
// from the DNA centre, the enemy is stunned for stunDuration seconds before resuming.
//
// Server-driven: only the server runs AI logic. NetworkTransform replicates position to clients.
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(NetworkTransform))]
public class NHEJEnemyAI : NetworkBehaviour
{
    enum EnemyState { Idle, Seeking, Stealing, Stunned }

    [Header("Movement")]
    [SerializeField] float moveSpeed = 0.5f;
    [SerializeField] float grabDistance = 0.15f;    // reach to "grab" a protein
    [SerializeField] float idleDuration = 2.5f;     // seconds between steal attempts
    [SerializeField] float dnaOrbitRadius = 0.55f;
    [SerializeField] float dnaOrbitHeight = 0.4f;
    [SerializeField] float dnaOrbitSpeed = 25f;     // degrees per second while idle

    [Header("Stun")]
    [SerializeField] float stunDistance = 2.5f;     // metres from DNA centre to trigger stun
    [SerializeField] float stunDuration = 6f;

    [Header("Steal")]
    [SerializeField] float stealDropRadius = 3f;    // metres from snap target to drop protein
    [SerializeField] float carryHeight = 0.25f;     // Y offset while carrying protein

    EnemyState currentState = EnemyState.Idle;
    float stateTimer;
    float orbitAngle;
    Vector3 dnaCenter;
    ProteinOrbitController targetProtein;
    Vector3 dropPosition;

    // Tracked on both server and grabbing client.
    bool isGrabbedByPlayer;

    XRGrabInteractable grab;

    void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnPlayerGrab);
            grab.selectExited.AddListener(OnPlayerRelease);
        }

        if (NHEJManager.Instance != null
            && NHEJManager.Instance.LeftDNAEnd != null
            && NHEJManager.Instance.RightDNAEnd != null)
        {
            dnaCenter = (NHEJManager.Instance.LeftDNAEnd.position
                       + NHEJManager.Instance.RightDNAEnd.position) * 0.5f;
        }

        if (IsServer)
        {
            stateTimer = idleDuration;
            currentState = EnemyState.Idle;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        // Release any protein being carried before we despawn.
        if (IsServer && targetProtein != null)
        {
            targetProtein.EnemyRelease(transform.position);
            targetProtein = null;
        }
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnPlayerGrab);
            grab.selectExited.RemoveListener(OnPlayerRelease);
        }
    }

    void Update()
    {
        if (!IsServer || isGrabbedByPlayer) return;

        switch (currentState)
        {
            case EnemyState.Idle:    UpdateIdle();    break;
            case EnemyState.Seeking: UpdateSeeking(); break;
            case EnemyState.Stealing: UpdateStealing(); break;
            case EnemyState.Stunned: UpdateStunned(); break;
        }
    }

    // ── State updates ─────────────────────────────────────────────────────────

    void UpdateIdle()
    {
        // Orbit lazily around the DNA break site.
        orbitAngle = (orbitAngle + dnaOrbitSpeed * Time.deltaTime) % 360f;
        float rad = orbitAngle * Mathf.Deg2Rad;
        transform.position = dnaCenter + new Vector3(
            Mathf.Cos(rad) * dnaOrbitRadius,
            dnaOrbitHeight,
            Mathf.Sin(rad) * dnaOrbitRadius);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
            SeekTarget();
    }

    void UpdateSeeking()
    {
        // Abort if the target was moved away from its correct position already.
        if (targetProtein == null || !targetProtein.IsCorrectlyPlaced)
        {
            EnterIdle();
            return;
        }

        Vector3 targetPos = targetProtein.transform.position;
        transform.position = Vector3.MoveTowards(
            transform.position, targetPos, moveSpeed * Time.deltaTime);

        if (transform.position != targetPos)
            transform.LookAt(targetPos);

        if (Vector3.Distance(transform.position, targetPos) < grabDistance)
            BeginSteal();
    }

    void UpdateStealing()
    {
        if (targetProtein == null) { EnterIdle(); return; }

        // Move toward drop position, dragging the protein along.
        transform.position = Vector3.MoveTowards(
            transform.position, dropPosition, moveSpeed * Time.deltaTime);
        targetProtein.transform.position = transform.position + Vector3.up * carryHeight;

        if (Vector3.Distance(transform.position, dropPosition) < 0.1f)
        {
            targetProtein.EnemyRelease(dropPosition);
            targetProtein = null;
            EnterIdle();
        }
    }

    void UpdateStunned()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
            EnterIdle();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void EnterIdle()
    {
        currentState = EnemyState.Idle;
        stateTimer = idleDuration;
    }

    void SeekTarget()
    {
        // Collect all correctly-placed proteins and pick one at random.
        var proteins = FindObjectsOfType<ProteinOrbitController>();
        var candidates = new System.Collections.Generic.List<ProteinOrbitController>();
        foreach (var p in proteins)
            if (p != null && p.IsCorrectlyPlaced) candidates.Add(p);

        if (candidates.Count == 0) { EnterIdle(); return; }

        targetProtein = candidates[Random.Range(0, candidates.Count)];
        currentState = EnemyState.Seeking;
    }

    void BeginSteal()
    {
        if (targetProtein == null) { EnterIdle(); return; }

        // Choose a drop position ~stealDropRadius metres from the snap target.
        Vector3 snapPos = targetProtein.SnapTargetPosition;
        Vector2 rnd = Random.insideUnitCircle.normalized;
        dropPosition = snapPos + new Vector3(rnd.x, 0f, rnd.y) * stealDropRadius;

        targetProtein.EnemyStartCarrying();
        currentState = EnemyState.Stealing;
    }

    // ── Player grab events ────────────────────────────────────────────────────

    void OnPlayerGrab(SelectEnterEventArgs _)
    {
        isGrabbedByPlayer = true;
        NotifyGrabbedServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void NotifyGrabbedServerRpc(ServerRpcParams rpcParams = default)
    {
        isGrabbedByPlayer = true;

        // If carrying a protein, release it in place so it isn't stuck while player drags enemy.
        if (targetProtein != null)
        {
            targetProtein.EnemyRelease(targetProtein.transform.position);
            targetProtein = null;
        }

        // Transfer ownership so the XR hand attachment drives position.
        NetworkObject.ChangeOwnership(rpcParams.Receive.SenderClientId);
    }

    void OnPlayerRelease(SelectExitEventArgs _)
    {
        if (!isGrabbedByPlayer) return; // only the grabbing client should act
        isGrabbedByPlayer = false;

        float dist = Vector3.Distance(transform.position, dnaCenter);
        NotifyReleasedServerRpc(transform.position, dist > stunDistance);
    }

    [ServerRpc(RequireOwnership = false)]
    void NotifyReleasedServerRpc(Vector3 releasePos, bool shouldStun)
    {
        isGrabbedByPlayer = false;
        NetworkObject.ChangeOwnership(NetworkManager.ServerClientId);
        transform.position = releasePos;

        if (shouldStun)
        {
            currentState = EnemyState.Stunned;
            stateTimer = stunDuration;
            Debug.Log($"[NHEJEnemy] Stunned for {stunDuration}s");
        }
        else
        {
            // Dropped close to DNA — resume idle patrol.
            EnterIdle();
        }
    }
}
