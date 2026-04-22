using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach to walls spawned via the NHEJSpawnPanel UI.
//
// States:
//   Free    — physics active, player can grab and position it
//   Pending — within snap radius of one or more DNASealPoints, glowing to indicate ready to bond
//   Sealed  — locked in place (kinematic), gets its own DNASealPoint for chaining
//
// Uses a proximity check in Update() — no trigger collider required.
// A wall can be pending on MULTIPLE seal points simultaneously so that a single
// piece can bridge two anchors: seal to anchor A first, then spray anchor C to
// complete the second bond without moving the wall.

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
    // Tracks whether seal visuals + physics have been applied locally — used as
    // Update guard so proximity logic stops immediately after sealing, before
    // the server's netState echo arrives.
    bool isVisuallySealed;

    // All DNASealPoints currently within snapRadius (can be more than one).
    readonly HashSet<DNASealPoint> contactedSealPoints = new();

    DNASealPoint sealedBy;
    bool         sealedOnRight;
    DNASealPoint ownSealPoint;
    Renderer[]   renderers;
    Material[]   originalMaterials;
    Rigidbody    rb;
    XRGrabInteractable grab;

    public GameManager gameManager;
    public static bool firstPlacement = true;

    /// <summary>True once seal physics have been applied locally (independent of netState timing).</summary>
    public bool IsSealed => isVisuallySealed;

    /// <summary>True if sealed but nothing has been sealed onto this wall yet.</summary>
    public bool IsLeaf => CurrentState == State.Sealed &&
                          (ownSealPoint == null || (!ownSealPoint.LeftSealed && !ownSealPoint.RightSealed));

    /// <summary>The DNASealPoint added to this wall after sealing — the next node in the bridge graph.</summary>
    public DNASealPoint OwnSealPoint => ownSealPoint;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        rb                = GetComponent<Rigidbody>();
        grab              = GetComponentInChildren<XRGrabInteractable>();
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
        if (isVisuallySealed) return;

        // Gather ALL seal points within snap radius (not just nearest).
        // This lets a wall be pending on both gap anchors at once so the player
        // can spray each side independently.
        var newContacts = new HashSet<DNASealPoint>();
        foreach (var sp in FindObjectsOfType<DNASealPoint>())
        {
            if (sp.gameObject == gameObject) continue;
            float d = Vector3.Distance(transform.position, sp.transform.position);
            if (d < snapRadius) newContacts.Add(sp);
        }

        // Notify seal points we left.
        foreach (var sp in contactedSealPoints)
            if (!newContacts.Contains(sp)) sp.NotifyContactEnd(this);

        // Notify seal points we newly entered.
        foreach (var sp in newContacts)
            if (!contactedSealPoints.Contains(sp))
            {
                bool isRight = sp.IsRightSide(transform.position);
                sp.NotifyContact(this, isRight);
            }

        contactedSealPoints.Clear();
        foreach (var sp in newContacts) contactedSealPoints.Add(sp);

        bool isNearAny = contactedSealPoints.Count > 0;
        SetVisualState(isNearAny ? State.Pending : State.Free);

        if (isNearAny && firstPlacement)
        {
            firstPlacement = false;
            gameManager.playPhase(3);
        }
    }

    // ── Called by DNASealPoint when spray can seals this wall ─────────────────

    public void OnSealed(DNASealPoint by)
    {
        sealedBy      = by;
        sealedOnRight = by.IsRightSide(transform.position);
        // Do NOT clear contactedSealPoints — other seal points still hold this
        // wall as pending so the player can spray them to complete the second bond.

        ApplySealPhysics();
        AddOwnSealPoint(by);
        SetVisualState(State.Sealed);
        isVisuallySealed = true;

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
        // Find the nearest seal point at the time of the network echo.
        DNASealPoint sp = null;
        float best = float.MaxValue;
        foreach (var candidate in FindObjectsOfType<DNASealPoint>())
        {
            if (candidate.gameObject == gameObject) continue;
            float d = Vector3.Distance(transform.position, candidate.transform.position);
            if (d < best) { best = d; sp = candidate; }
        }

        if (sp != null)
        {
            sealedBy      = sp;
            sealedOnRight = sp.IsRightSide(transform.position);
        }
        ApplySealPhysics();
        if (sp != null) AddOwnSealPoint(sp);
        SetVisualState(State.Sealed);
        isVisuallySealed = true;
    }

    void ApplySealPhysics()
    {
        rb.isKinematic = true;
        rb.useGravity  = false;
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
        Debug.Log($"[SpawnedDNAWall] {gameObject.name}: OwnSealPoint created at {transform.position} (inherited axis from {by.gameObject.name})");
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

        // Clear this wall from any seal point that still has it as pending.
        foreach (var sp in FindObjectsOfType<DNASealPoint>())
            sp.NotifyContactEnd(this);
        contactedSealPoints.Clear();

        ApplyDetachPhysics();
        SetVisualState(State.Free);
        isVisuallySealed = false;
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
        foreach (var sp in FindObjectsOfType<DNASealPoint>())
            sp.NotifyContactEnd(this);
        contactedSealPoints.Clear();
        ApplyDetachPhysics();
        SetVisualState(State.Free);
        isVisuallySealed = false;
    }

    void ApplyDetachPhysics()
    {
        rb.isKinematic = false;
        rb.useGravity  = true;
        if (grab != null) grab.enabled = true;
        gameObject.tag = "Untagged";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetVisualState(State next)
    {
        Material target = next == State.Pending && pendingMaterial != null
            ? pendingMaterial
            : null;

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material = target ?? originalMaterials[i];
    }
}
