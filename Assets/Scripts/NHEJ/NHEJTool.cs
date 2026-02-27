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
        // Both Trimmer and Ligase now interact via placement on release,
        // not via trigger. See ArtemisOrbitController / LigaseOrbitController.
    }
}
