using UnityEngine;

// Captures the DNA piece's localScale at Awake — i.e., the prefab's intended
// visual size — before any grab/socket logic can warp it. DNARepairSocket reads
// this when restoring scale on socket entry, so the piece always ends up at
// its real intended size regardless of who owns it or what their parent chain
// looks like.
public class DNAPieceRestScale : MonoBehaviour
{
    public Vector3 RestScale { get; private set; } = Vector3.one;

    void Awake()
    {
        RestScale = transform.localScale;
    }
}
