using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach to walls spawned via the NHEJSpawnPanel UI.
//
// States:
//   Free    — physics active, player can grab and position it
//   Pending — within snap radius of a DNASealPoint, glowing to indicate ready to bond
//   Sealed  — locked in place (kinematic), gets its own DNASealPoint for chaining
//
// Uses a proximity check in Update() — no trigger collider required.
// The existing wall just needs a DNASealPoint component added to it.

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class SpawnedDNAWall : NetworkBehaviour
{
    [Tooltip("How close this wall needs to be to a DNASealPoint before it registers as pending.")]
    [SerializeField] float snapRadius = 0.3f;

    [Tooltip("Glow applied to THIS wall while it is near a seal point.")]
    [SerializeField] Material pendingMaterial;

    public enum State { Free, Pending, Sealed }

    // ── Networked state ───────────────────────────────────────────────────────
    // Server-authoritative: only Free (0) and Sealed (2) are broadcast.
    // Pending is local-only visual feedback.

    readonly NetworkVariable<byte> netState = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public State CurrentState => (State)netState.Value;

    // Set on the client that initiated the seal, prevents double-apply when the
    // server's netState echo arrives on that same client.
    bool sealAppliedLocally;
    bool detachAppliedLocally;

    DNASealPoint currentSealPoint;
    DNASealPoint sealedBy;
    bool         sealedOnRight;
    DNASealPoint ownSealPoint;
    Renderer[]   renderers;
    Material[]   originalMaterials;
    Rigidbody    rb;

    public GameManager gameManager;
    public static bool firstPlacement = true;

    /// <summary>True if sealed but nothing has been sealed onto this wall yet.</summary>
    public bool IsLeaf => CurrentState == State.Sealed &&
                          (ownSealPoint == null || (!ownSealPoint.LeftSealed && !ownSealPoint.RightSealed));

    /// <summary>The DNASealPoint added to this wall after sealing — the next node in the bridge graph.</summary>
    public DNASealPoint OwnSealPoint => ownSealPoint;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        rb                = GetComponent<Rigidbody>();
        renderers         = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].material;

        gameManager = GameObject.FindGameObjectsWithTag("GameManager")[0].GetComponent<GameManager>();
    }

    public override void OnNetworkSpawn()
    {
        netState.OnValueChanged += OnNetStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        netState.OnValueChanged -= OnNetStateChanged;
    }

    // Runs on every client when server changes netState.
    void OnNetStateChanged(byte prev, byte curr)
    {
        var newState = (State)curr;

        if (newState == State.Sealed && (State)prev != State.Sealed)
        {
            if (sealAppliedLocally)
            {
                sealAppliedLocally = false;
                return; // this client already applied via OnSealed()
            }
            ApplySealLocally();
        }
        else if (newState == State.Free && (State)prev == State.Sealed)
        {
            if (detachAppliedLocally)
            {
                detachAppliedLocally = false;
                return; // this client already applied via Detach()
            }
            ApplyDetachLocally();
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (CurrentState == State.Sealed || sealAppliedLocally) return;

        DNASealPoint nearest = FindNearestSealPoint();

        if (nearest != currentSealPoint)
        {
            if (currentSealPoint != null)
                currentSealPoint.NotifyContactEnd(this);

            currentSealPoint = nearest;

            if (nearest != null)
            {
                bool isRight = nearest.IsRightSide(transform.position);
                nearest.NotifyContact(this, isRight);
                SetVisualState(State.Pending);

                // Timeline update if this is the first ligase
                if(firstPlacement){
                    firstPlacement = false;
                    gameManager.playPhase(3);
                }
            }
            else
            {
                SetVisualState(State.Free);
            }
        }
    }

    // ── Called by DNASealPoint when spray can seals this wall ─────────────────

    public void OnSealed(DNASealPoint by)
    {
        sealedBy      = by;
        sealedOnRight = by.IsRightSide(transform.position);
        currentSealPoint = null;

        ApplySealPhysics();
        AddOwnSealPoint(by);
        SetVisualState(State.Sealed);

        // Tell server — other clients will apply via OnNetStateChanged.
        sealAppliedLocally = true;
        if (IsSpawned) SealServerRpc();

        Debug.Log($"[SpawnedDNAWall] Sealed onto {by.gameObject.name}");
    }

    [ServerRpc(RequireOwnership = false)]
    void SealServerRpc()
    {
        netState.Value = (byte)State.Sealed;
    }

    // Called on non-owning clients via netState change.
    void ApplySealLocally()
    {
        var sp = FindNearestSealPoint();
        if (sp != null)
        {
            sealedBy      = sp;
            sealedOnRight = sp.IsRightSide(transform.position);
            currentSealPoint = null;
        }
        ApplySealPhysics();
        if (sp != null) AddOwnSealPoint(sp);
        SetVisualState(State.Sealed);
    }

    void ApplySealPhysics()
    {
        rb.isKinematic = true;
        rb.useGravity  = false;

        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null) grab.enabled = false;
    }

    void AddOwnSealPoint(DNASealPoint by)
    {
        if (ownSealPoint != null) return;
        ownSealPoint = gameObject.AddComponent<DNASealPoint>();
        ownSealPoint.helixAxisLocal  = by.helixAxisLocal;
        ownSealPoint.pendingMaterial = by.pendingMaterial;
        ownSealPoint.sealedMaterial  = by.sealedMaterial;
        gameObject.tag = "DNAWall";
    }

    // ── Detach ────────────────────────────────────────────────────────────────

    /// <summary>Detaches this wall from its seal point and returns it to a free state.</summary>
    public void Detach()
    {
        if (CurrentState != State.Sealed) return;

        if (sealedBy != null)
            sealedBy.UnsealSide(sealedOnRight);

        if (ownSealPoint != null)
        {
            Destroy(ownSealPoint);
            ownSealPoint = null;
        }

        ApplyDetachPhysics();
        SetVisualState(State.Free);
        sealedBy = null;

        // Tell server — other clients will apply via OnNetStateChanged.
        detachAppliedLocally = true;
        if (IsSpawned) DetachServerRpc();

        Debug.Log($"[SpawnedDNAWall] Detached from seal point.");
    }

    [ServerRpc(RequireOwnership = false)]
    void DetachServerRpc()
    {
        netState.Value = (byte)State.Free;
    }

    // Called on non-owning clients via netState change.
    void ApplyDetachLocally()
    {
        if (sealedBy != null) sealedBy.UnsealSide(sealedOnRight);
        if (ownSealPoint != null) { Destroy(ownSealPoint); ownSealPoint = null; }
        sealedBy = null;
        ApplyDetachPhysics();
        SetVisualState(State.Free);
    }

    void ApplyDetachPhysics()
    {
        rb.isKinematic = false;
        rb.useGravity  = true;

        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null) grab.enabled = true;

        gameObject.tag = "Untagged";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    DNASealPoint FindNearestSealPoint()
    {
        DNASealPoint nearest = null;
        float nearestDist    = snapRadius;

        foreach (var sp in FindObjectsOfType<DNASealPoint>())
        {
            if (sp.gameObject == gameObject) continue;
            float d = Vector3.Distance(transform.position, sp.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = sp; }
        }
        return nearest;
    }

    void SetVisualState(State next)
    {
        Material target = next == State.Pending && pendingMaterial != null
            ? pendingMaterial
            : null;

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material = target ?? originalMaterials[i];
    }
}
