using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Drives the Artemis tool in a circular orbit around the DNA break site.
// Orbit pauses when a player grabs the tool.
// Cutting is now handled by ArtemisBlade (child component with trigger collider) — the
// snap-to-TrimPoint release mechanic is disabled but preserved below for easy reversion.
[RequireComponent(typeof(NHEJTool))]
public class ArtemisOrbitController : NetworkBehaviour
{
    [Header("Orbit")]
    [SerializeField] float orbitRadius = 0.35f;
    [SerializeField] float orbitSpeed = 45f;    // degrees per second
    [SerializeField] float orbitHeight = 0.05f; // Y above DNA midpoint

    [Header("Placement")]
    [SerializeField] float snapRadius = 0.12f;

    // Orbit state (owner-driven; synced to others via NetworkTransform on NHEJTool)
    Vector3 orbitCenterPos;
    float currentAngle;
    bool isGrabbed;
    bool isPlaced;

    TrimPoint[] trimPoints;
    NHEJTool tool;
    XRGrabInteractable grab;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        tool = GetComponent<NHEJTool>();
        grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);

        // Compute orbit center from the DNA ends managed by NHEJManager
        if (NHEJManager.Instance != null
            && NHEJManager.Instance.LeftDNAEnd != null
            && NHEJManager.Instance.RightDNAEnd != null)
        {
            orbitCenterPos = (NHEJManager.Instance.LeftDNAEnd.position
                            + NHEJManager.Instance.RightDNAEnd.position) * 0.5f;
        }

        // FindObjectsOfType excludes inactive GameObjects by default,
        // so deactivated TrimPoints (wrong scenario) are already filtered out.
        trimPoints = FindObjectsOfType<TrimPoint>();

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
        // Only the current owner drives the orbit position;
        // NetworkTransform on NHEJTool replicates it to all clients.
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
        // Face the DNA center while flying
        transform.LookAt(orbitCenterPos);
    }

    void OnGrabbed(SelectEnterEventArgs _) => isGrabbed = true;

    void OnReleased(SelectExitEventArgs _)
    {
        isGrabbed = false;
        // Orbit resumes automatically in Update() since isGrabbed is now false.
        // Cut detection is handled by ArtemisBlade (child trigger collider) — no snap needed here.

        /* DISABLED (snap-to-TrimPoint — preserved for reversion):
        if (isPlaced) return;
        TrimPoint closest = null;
        float bestDist = snapRadius;
        if (trimPoints != null)
        {
            foreach (var tp in trimPoints)
            {
                if (tp == null || tp.IsTrimmed || !tp.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(transform.position, tp.transform.position);
                if (d < bestDist) { bestDist = d; closest = tp; }
            }
        }
        if (closest != null)
        {
            isPlaced = true;
            transform.position = closest.transform.position;
            closest.OnToolActivated(NetworkManager.Singleton.LocalClientId);
            ResetOrbitServerRpc();
        }
        */
    }

    [ServerRpc(RequireOwnership = false)]
    void ResetOrbitServerRpc()
    {
        StartCoroutine(DelayedReset());
    }

    System.Collections.IEnumerator DelayedReset()
    {
        yield return new WaitForSeconds(0.75f);
        ResetOrbitClientRpc();
    }

    [ClientRpc]
    void ResetOrbitClientRpc()
    {
        isPlaced = false;
    }
}
