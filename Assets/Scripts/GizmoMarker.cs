using UnityEngine;

public class GizmoMarker : MonoBehaviour
{
    public Color color = Color.yellow;
    public float radius = 0.05f;
    public bool drawLabel = true;

    void OnDrawGizmos()
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.DrawSphere(transform.position, radius * 0.4f);

        if (drawLabel)
        {
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(transform.position + Vector3.up * (radius + 0.02f), gameObject.name);
        }
    }
}
