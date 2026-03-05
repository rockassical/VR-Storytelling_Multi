using UnityEngine;

// Visual scene marker indicating where a player should place their orbiting protein.
// Phase handlers reference these Transforms via serialized fields to pass snap positions
// to ProteinOrbitController.Configure(). No runtime logic — purely a placement guide.
public class ProteinPlacementPoint : MonoBehaviour
{
    [Tooltip("Which player role this target is for (1 = P1/p53, 2 = P2/ATM)")]
    public int playerRole = 1;

    void OnDrawGizmos()
    {
        Gizmos.color = playerRole == 1
            ? new Color(0.2f, 0.6f, 1f, 0.55f)
            : new Color(0.2f, 1f, 0.4f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = playerRole == 1 ? Color.cyan : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.20f);
    }
}
