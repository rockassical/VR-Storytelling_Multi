using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Drives the Ligase IV tool in a circular orbit around the DNA break site.
// Pauses when grabbed; on release, snaps to the nearest unsealed LigationPoint
// within snapRadius and fires the ligation. Otherwise resumes orbiting.
[RequireComponent(typeof(NHEJTool))]
public class LigaseOrbitController : NetworkBehaviour
{
    [Header("Orbit")]
    [SerializeField] float orbitRadius = 0.35f;
    [SerializeField] float orbitSpeed = 45f;    // degrees per second
    [SerializeField] float orbitHeight = 0.05f; // Y above DNA midpoint

    [Header("Placement")]
    [SerializeField] float snapRadius = 0.12f;

    Vector3 orbitCenterPos;
    float currentAngle;
    bool isGrabbed;
    bool isPlaced;

    LigationPoint[] ligationPoints;
    NHEJTool tool;
    XRGrabInteractable grab;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        tool = GetComponent<NHEJTool>();
        grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);

        if (NHEJManager.Instance != null
            && NHEJManager.Instance.LeftDNAEnd != null
            && NHEJManager.Instance.RightDNAEnd != null)
        {
            orbitCenterPos = (NHEJManager.Instance.LeftDNAEnd.position
                            + NHEJManager.Instance.RightDNAEnd.position) * 0.5f;
        }

        // FindObjectsOfType excludes inactive GameObjects, so any deactivated
        // LigationPoints are already filtered out.
        ligationPoints = FindObjectsOfType<LigationPoint>();

        currentAngle = 0f;
        if (IsOwner)
            ApplyOrbitPosition();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }
    }

    void Update()
    {
        if (!IsOwner || isGrabbed || isPlaced) return;

        currentAngle = (currentAngle + orbitSpeed * Time.deltaTime) % 360f;
        ApplyOrbitPosition();
    }

    void ApplyOrbitPosition()
    {
        float rad = currentAngle * Mathf.Deg2Rad;
        transform.position = orbitCenterPos + new Vector3(
            Mathf.Cos(rad) * orbitRadius,
            orbitHeight,
            Mathf.Sin(rad) * orbitRadius);
        transform.LookAt(orbitCenterPos);
    }

    void OnGrabbed(SelectEnterEventArgs _) => isGrabbed = true;

    void OnReleased(SelectExitEventArgs _)
    {
        isGrabbed = false;
        if (isPlaced) return;

        LigationPoint closest = null;
        float bestDist = snapRadius;

        if (ligationPoints != null)
        {
            foreach (var lp in ligationPoints)
            {
                if (lp == null || lp.IsSealed || !lp.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(transform.position, lp.transform.position);
                if (d < bestDist) { bestDist = d; closest = lp; }
            }
        }

        if (closest != null)
        {
            isPlaced = true;
            transform.position = closest.transform.position;
            closest.OnToolActivated(NetworkManager.Singleton.LocalClientId);
        }
        // If not close enough: isGrabbed=false, isPlaced=false → orbit resumes in Update
    }
}
