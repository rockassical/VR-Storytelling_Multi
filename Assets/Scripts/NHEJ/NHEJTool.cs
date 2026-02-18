using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRMultiplayer;


[RequireComponent(typeof(XRGrabInteractable))]
public class NHEJTool : NetworkBaseInteractable
{
    public enum ToolType { Trimmer, Ligase }

    [Header("NHEJ Tool Settings")]
    [SerializeField] ToolType toolType = ToolType.Trimmer;
    [SerializeField] float detectionRadius = 0.15f;
    [SerializeField] Transform tipTransform;

    public ToolType CurrentToolType => toolType;

    public override void Activated(bool activate)
    {
        base.Activated(activate);

        if (!activate) return;
        if (!IsOwner) return;

        Vector3 origin = tipTransform != null ? tipTransform.position : transform.position;
        Vector3 forward = tipTransform != null ? tipTransform.forward : transform.forward;

        // SphereCast to find interaction points
        if (Physics.SphereCast(origin, detectionRadius, forward, out RaycastHit hit, detectionRadius * 2f))
        {
            if (toolType == ToolType.Trimmer && hit.collider.TryGetComponent(out TrimPoint trimPoint))
            {
                trimPoint.OnToolActivated(OwnerClientId);
            }
            else if (toolType == ToolType.Ligase && hit.collider.TryGetComponent(out LigationPoint ligationPoint))
            {
                ligationPoint.OnToolActivated(OwnerClientId);
            }
        }

        // Also check overlap sphere for very close proximity
        var colliders = Physics.OverlapSphere(origin, detectionRadius);
        foreach (var col in colliders)
        {
            if (toolType == ToolType.Trimmer && col.TryGetComponent(out TrimPoint tp))
            {
                tp.OnToolActivated(OwnerClientId);
                break;
            }
            else if (toolType == ToolType.Ligase && col.TryGetComponent(out LigationPoint lp))
            {
                lp.OnToolActivated(OwnerClientId);
                break;
            }
        }
    }
}
