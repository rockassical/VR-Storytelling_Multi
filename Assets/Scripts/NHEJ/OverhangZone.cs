using UnityEngine;

// Place this on a GameObject with a BoxCollider (IsTrigger = true) that covers the overhang region.
// ArtemisBlade detects when the blade tip enters this zone and fires the cut.
//
// Scene setup:
//   - Create an empty child of the DNA root named "LeftOverhangZone" (or "RightOverhangZone").
//   - Add a BoxCollider, tick IsTrigger.
//   - Size/position it to loosely cover the overhang strands.
//   - Add this component and assign the cutter + player role.
[RequireComponent(typeof(Collider))]
public class OverhangZone : MonoBehaviour
{
    [Tooltip("1 = Player 1 (left end), 2 = Player 2 (right end).")]
    public int playerRole = 1;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning("[OverhangZone] Collider was not a trigger — fixed automatically.");
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Semi-transparent box to visualise zone bounds in editor
        var col = GetComponent<BoxCollider>();
        if (col == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = playerRole == 1
            ? new Color(0.2f, 0.8f, 1f, 0.25f)
            : new Color(1f, 0.5f, 0.1f, 0.25f);
        Gizmos.DrawCube(col.center, col.size);
        Gizmos.color = playerRole == 1
            ? new Color(0.2f, 0.8f, 1f, 0.8f)
            : new Color(1f, 0.5f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(col.center, col.size);
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
