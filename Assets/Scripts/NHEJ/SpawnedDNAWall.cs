using UnityEngine;

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
public class SpawnedDNAWall : MonoBehaviour
{
    [Tooltip("How close this wall needs to be to a DNASealPoint before it registers as pending.")]
    [SerializeField] float snapRadius = 0.3f;

    [Tooltip("Glow applied to THIS wall while it is near a seal point.")]
    [SerializeField] Material pendingMaterial;

    public enum State { Free, Pending, Sealed }
    public State CurrentState { get; private set; } = State.Free;

    DNASealPoint currentSealPoint;
    Renderer[]   renderers;
    Material[]   originalMaterials;
    Rigidbody    rb;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        rb                = GetComponent<Rigidbody>();
        renderers         = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].material;
    }

    void Update()
    {
        if (CurrentState == State.Sealed) return;

        DNASealPoint nearest = FindNearestSealPoint();

        if (nearest != currentSealPoint)
        {
            // Left old seal point
            if (currentSealPoint != null)
                currentSealPoint.NotifyContactEnd(this);

            currentSealPoint = nearest;

            if (nearest != null)
            {
                bool isRight = nearest.IsRightSide(transform.position);
                nearest.NotifyContact(this, isRight);
                SetState(State.Pending);
            }
            else
            {
                SetState(State.Free);
            }
        }
    }

    // ── Called by DNASealPoint when spray can seals this wall ─────────────────

    public void OnSealed(DNASealPoint by)
    {
        currentSealPoint = null;
        SetState(State.Sealed);

        rb.isKinematic = true;
        rb.useGravity  = false;

        var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null) grab.enabled = false;

        // Inherit a DNASealPoint so further spawned walls can chain onto this one.
        var newSealPoint = gameObject.AddComponent<DNASealPoint>();
        newSealPoint.helixAxisLocal  = by.helixAxisLocal;
        newSealPoint.pendingMaterial = by.pendingMaterial;
        newSealPoint.sealedMaterial  = by.sealedMaterial;

        gameObject.tag = "DNAWall";

        Debug.Log($"[SpawnedDNAWall] Sealed onto {by.gameObject.name}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    DNASealPoint FindNearestSealPoint()
    {
        DNASealPoint nearest = null;
        float nearestDist    = snapRadius;

        foreach (var sp in FindObjectsOfType<DNASealPoint>())
        {
            // Don't snap to a seal point on ourselves (after sealing, we get one)
            if (sp.gameObject == gameObject) continue;

            float d = Vector3.Distance(transform.position, sp.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = sp; }
        }
        return nearest;
    }

    void SetState(State next)
    {
        CurrentState = next;

        Material target = next == State.Pending && pendingMaterial != null
            ? pendingMaterial
            : null;

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material = target ?? originalMaterials[i];
    }
}
