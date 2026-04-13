using UnityEngine;

// Attach to every existing DNAWall (the ones with DNAPair) in the scene.
// Also added at runtime to SpawnedDNAWalls after they are sealed, so chains of
// walls can form: existing → spawned → spawned → ...
//
// Manages two attachment slots (left and right along the helix axis).
// When a SpawnedDNAWall enters contact, the matching side glows blue ("bond here").
// When the LigaseIV spray can hits this wall, the pending spawned wall is locked in.
// Two walls can be pending simultaneously (one per side).
public class DNASealPoint : MonoBehaviour
{
    [Tooltip("The local axis the helix runs along — used to determine left vs right side. Match the value set on NHEJBreakPoint.")]
    [SerializeField] public Vector3 helixAxisLocal = Vector3.right;

    [Tooltip("Applied to this wall while a spawned wall is pending on either side.")]
    [SerializeField] public Material pendingMaterial;   // blue/cyan glow

    [Tooltip("Applied to this wall once both pending walls are sealed (optional visual confirmation).")]
    [SerializeField] public Material sealedMaterial;    // gold/white glow

    // Left = negative helix axis direction, Right = positive.
    SpawnedDNAWall leftPending;
    SpawnedDNAWall rightPending;

    Renderer[]  renderers;
    Material[]  originalMaterials;
    bool        leftSealed;
    bool        rightSealed;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        renderers        = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].material;
    }

    // ── Called by SpawnedDNAWall ──────────────────────────────────────────────

    /// <summary>A spawned wall has moved into contact range on the given side.</summary>
    public void NotifyContact(SpawnedDNAWall wall, bool isRightSide)
    {
        if (isRightSide)
        {
            if (rightSealed) return;
            rightPending = wall;
        }
        else
        {
            if (leftSealed) return;
            leftPending = wall;
        }
        RefreshGlow();
    }

    /// <summary>A spawned wall has left contact range or been picked up again.</summary>
    public void NotifyContactEnd(SpawnedDNAWall wall)
    {
        if (leftPending  == wall) leftPending  = null;
        if (rightPending == wall) rightPending = null;
        RefreshGlow();
    }

    // ── Called by LigaseSprayCan ──────────────────────────────────────────────

    /// <summary>
    /// Returns true if there is at least one pending (unsealed) wall ready to bond.
    /// </summary>
    public bool HasPending => (leftPending != null && !leftSealed)
                           || (rightPending != null && !rightSealed);

    /// <summary>
    /// Seals whichever pending walls are currently in contact.
    /// Called by LigaseSprayCan when it sprays near this wall.
    /// </summary>
    public void Seal()
    {
        if (leftPending != null && !leftSealed)
        {
            leftPending.OnSealed(this);
            leftSealed  = true;
            leftPending = null;
        }

        if (rightPending != null && !rightSealed)
        {
            rightPending.OnSealed(this);
            rightSealed  = true;
            rightPending = null;
        }

        RefreshGlow();
    }

    // ── Glow ─────────────────────────────────────────────────────────────────

    void RefreshGlow()
    {
        bool anyPending = leftPending != null || rightPending != null;
        bool allSealed  = leftSealed && rightSealed;

        Material target = allSealed && sealedMaterial != null ? sealedMaterial
                        : anyPending && pendingMaterial != null ? pendingMaterial
                        : null;

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material = target != null ? target : originalMaterials[i];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Determines which side of this wall a contact point is on.
    /// Returns true = right (positive helix axis), false = left.
    /// </summary>
    public bool IsRightSide(Vector3 contactWorldPos)
    {
        Vector3 axisWorld = transform.TransformDirection(helixAxisLocal).normalized;
        Vector3 toContact = contactWorldPos - transform.position;
        return Vector3.Dot(toContact, axisWorld) > 0f;
    }
}
