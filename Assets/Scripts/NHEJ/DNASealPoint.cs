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

    // Stored so the graph BFS can traverse the seal chain.
    [SerializeField] SpawnedDNAWall leftSealedWall;
    [SerializeField] SpawnedDNAWall rightSealedWall;

    Renderer[]  renderers;
    Material[]  originalMaterials;
    bool        leftSealed;
    bool        rightSealed;

    static bool firstPlacement = true;
    static bool firstLigase = true;
    public GameManager gameManager;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        // When added at runtime to a SpawnedDNAWall, skip renderer capture entirely —
        // the wall manages its own visuals. Capturing here would let RefreshGlow()
        // overwrite the sealed wall's material with the pending colour.
        if (GetComponent<SpawnedDNAWall>() != null)
        {
            renderers         = new Renderer[0];
            originalMaterials = new Material[0];
            return;
        }

        renderers        = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].material;

        gameManager = GameObject.FindGameObjectsWithTag("GameManager")[0].GetComponent<GameManager>();
    }

    // ── Called by SpawnedDNAWall ──────────────────────────────────────────────

    /// <summary>A spawned wall has moved into contact range on the given side.</summary>
    public void NotifyContact(SpawnedDNAWall wall, bool isRightSide)
    {
        if (isRightSide)
        {
            if (rightSealed) { Debug.Log($"[SealPoint] {gameObject.name}: rightSealed=true, ignoring {wall.gameObject.name}"); return; }
            rightPending = wall;
        }
        else
        {
            if (leftSealed) { Debug.Log($"[SealPoint] {gameObject.name}: leftSealed=true, ignoring {wall.gameObject.name}"); return; }
            leftPending = wall;
        }
        Debug.Log($"[SealPoint] {gameObject.name}: {wall.gameObject.name} pending on {(isRightSide ? "RIGHT" : "LEFT")} side");
        RefreshGlow();

        // Timeline update if this is the first placement
        if(firstPlacement){
            firstPlacement = false;
            gameManager.playPhase(3);
        }
    }

    /// <summary>A spawned wall has left contact range or been picked up again.</summary>
    public void NotifyContactEnd(SpawnedDNAWall wall)
    {
        if (leftPending  == wall) { leftPending  = null; Debug.Log($"[SealPoint] {gameObject.name}: {wall.gameObject.name} left LEFT contact"); }
        if (rightPending == wall) { rightPending = null; Debug.Log($"[SealPoint] {gameObject.name}: {wall.gameObject.name} left RIGHT contact"); }
        RefreshGlow();
    }

    // ── Called by LigaseSprayCan ──────────────────────────────────────────────

    /// <summary>
    /// Returns true if there is at least one pending (unsealed) wall ready to bond.
    /// </summary>
    public bool HasPending  => (leftPending  != null && !leftSealed)
                           || (rightPending != null && !rightSealed);
    public bool LeftSealed  => leftSealed;
    public bool RightSealed => rightSealed;

    /// <summary>Returns the walls sealed onto this node for graph traversal.</summary>
    public SpawnedDNAWall LeftSealedWall  => leftSealedWall;
    public SpawnedDNAWall RightSealedWall => rightSealedWall;

    /// <summary>
    /// Seals whichever pending walls are currently in contact.
    /// Called by LigaseSprayCan when it sprays near this wall.
    /// </summary>
    public void Seal()
    {
        if (leftPending != null && !leftSealed)
        {
            leftSealedWall = leftPending;
            Debug.Log($"[SealPoint] {gameObject.name}: sealing {leftPending.gameObject.name} on LEFT (alreadySealed={leftPending.IsSealed})");
            if (!leftPending.IsSealed)
                leftPending.OnSealed(this);
            // If already sealed to the other anchor, just register it in the graph —
            // the wall is kinematic/frozen in place and its OwnSealPoint is already live.
            leftSealed  = true;
            leftPending = null;
        }

        if (rightPending != null && !rightSealed)
        {
            rightSealedWall = rightPending;
            Debug.Log($"[SealPoint] {gameObject.name}: sealing {rightPending.gameObject.name} on RIGHT (alreadySealed={rightPending.IsSealed})");
            if (!rightPending.IsSealed)
                rightPending.OnSealed(this);
            rightSealed  = true;
            rightPending = null;
        }

        Debug.Log($"[SealPoint] {gameObject.name}: after Seal — L={leftSealedWall?.gameObject.name} R={rightSealedWall?.gameObject.name}");
        RefreshGlow();

        if(firstLigase){
            Debug.Log("THIS IS FIRST LIGASE! (DNASealPoint)");
            firstLigase = false;
            gameManager.playPhase(3);
        }
    }

    /// <summary>
    /// Network-replication hook: register a wall as sealed onto this point without
    /// going through Seal() (which would re-broadcast and require pending state).
    /// Called by SpawnedDNAWall.ApplySealLocally() on non-spraying clients so the
    /// BFS graph matches the spraying client's graph.
    /// </summary>
    public void RegisterSealedWall(SpawnedDNAWall wall, bool isRight)
    {
        if (isRight)
        {
            rightSealedWall = wall;
            rightSealed = true;
            rightPending = null;
        }
        else
        {
            leftSealedWall = wall;
            leftSealed = true;
            leftPending = null;
        }
        RefreshGlow();
    }

    /// <summary>Reverses a seal on the given side — called when Artemis cuts a sealed wall off.</summary>
    public void UnsealSide(bool isRight)
    {
        if (isRight) { rightSealed = false; rightPending = null; rightSealedWall = null; }
        else         { leftSealed  = false; leftPending  = null; leftSealedWall  = null; }
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

        Debug.Log($"[Glow] {gameObject.name}: anyPending={anyPending} allSealed={allSealed} pendingMatAssigned={pendingMaterial != null} renderers={renderers.Length} target={(target ? target.name : "null")}");

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
