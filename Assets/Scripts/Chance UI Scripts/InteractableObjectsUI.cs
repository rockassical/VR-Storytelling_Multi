using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactors; // Required for XRRayInteractor
using UnityEngine.EventSystems; // Required for RaycastResult (UI)

public class XRPointerInspector : MonoBehaviour
{
    [Header("Interactors to Watch")]
    public XRRayInteractor leftHandRay;
    public XRRayInteractor rightHandRay;

    [Header("UI Output (Optional)")]
    public TextMeshProUGUI infoLabel;

    [Header("Current Data (Read Only)")]
    [SerializeField] private GameObject currentObject;
    [SerializeField] private string objectTag;
    [SerializeField] private string objectLayer;

    private void Update()
    {
        // Check both hands, prioritizing the right hand
        if (!CheckPointer(rightHandRay))
        {
            if (!CheckPointer(leftHandRay))
            {
                ClearInfo();
            }
        }
    }

    private bool CheckPointer(XRRayInteractor interactor)
    {
        if (interactor == null) return false;

        // 1. Check for UI Hits (Canvas Buttons, Sliders, etc.)
        if (interactor.TryGetCurrentUIRaycastResult(out RaycastResult uiHit))
        {
            UpdateInfo(uiHit.gameObject);
            return true;
        }

        // 2. Check for 3D Hits (Cubes, Props, Doors)
        if (interactor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            UpdateInfo(hit.collider.gameObject);
            return true;
        }

        return false;
    }

    private void UpdateInfo(GameObject target)
    {
        // Only update logic if the object actually changed to save performance
        if (currentObject == target) return;

        currentObject = target;
        objectTag = target.tag;
        objectLayer = LayerMask.LayerToName(target.layer);

        string message = $"<b>Pointing At:</b> {target.name}\n" +
                        $"<b>Tag:</b> {objectTag}\n" +
                        $"<b>Layer:</b> {objectLayer}";

        if (infoLabel != null) infoLabel.text = message;

        Debug.Log($"Pointer is over: {target.name}");
    }

    private void ClearInfo()
    {
        if (currentObject == null) return;

        currentObject = null;
        objectTag = "";
        objectLayer = "";

        if (infoLabel != null) infoLabel.text = "Searching for targets...";
    }

    // Public helper method to let other scripts get the object easily
    public GameObject GetLookedAtObject() => currentObject;
}