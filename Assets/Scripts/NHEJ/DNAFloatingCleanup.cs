using UnityEngine;

// Attach to nucleotide GameObjects in the DNA helix.
// After a short delay, checks if this piece is isolated — no DNAWall-tagged segment
// is within proximityRadius. If so, it launches itself and self-destructs.
// This catches nucleotides left hanging at the edge of an explosion zone.
//
// Setup: Add this component to nucleotide prefabs/objects. Tune proximityRadius
// to roughly match the distance between adjacent wall and nucleotide in your model.
public class DNAFloatingCleanup : MonoBehaviour
{
    [Tooltip("Seconds to wait before checking — should be longer than the explosion stagger delay.")]
    [SerializeField] float checkDelay = 1.0f;

    [Tooltip("If no DNAWall is found within this world-space radius, this piece is considered floating.")]
    [SerializeField] float proximityRadius = 0.5f;

    [Tooltip("Impulse force applied when launching a floating piece.")]
    [SerializeField] float launchForce = 0.8f;

    [Tooltip("Seconds until the launched piece is destroyed.")]
    [SerializeField] float destroyDelay = 3f;

    /// <summary>
    /// Called by NHEJBreakPoint after the explosion fires.
    /// extraDelay is added on top of checkDelay to account for explosion stagger time.
    /// </summary>
    public void Activate(float extraDelay = 0f)
    {
        Invoke(nameof(CheckIfIsolated), checkDelay + extraDelay);
    }

    void CheckIfIsolated()
    {
        // If any DNAWall is still nearby, this piece is still attached — leave it alone.
        var walls = GameObject.FindGameObjectsWithTag("DNAWall");
        foreach (var wall in walls)
        {
            if (wall == null || wall == gameObject) continue;  // exclude self
            if (Vector3.Distance(transform.position, wall.transform.position) <= proximityRadius)
                return;
        }

        // No wall neighbour found — piece is floating, launch it.
        Launch();
    }

    void Launch()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity  = true;
        rb.AddForce(
            (Random.insideUnitSphere + Vector3.up * 0.5f).normalized * launchForce,
            ForceMode.Impulse);
        Destroy(gameObject, destroyDelay);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, proximityRadius);
    }
#endif
}
