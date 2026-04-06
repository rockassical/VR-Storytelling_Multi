using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class InteractableObjectsUI : MonoBehaviour
{
    public TextMeshProUGUI rightControllerUIText, leftControllerUIText;
    public XRInteractionGroup rightTargetGroup, leftTargetGroup;

    private Transform mainCameraTransform;
    //public Transform _rightControllerTransform, _leftControllerTransform, rightControllerUITransform, leftControllerUITranform;
    //public Transform interactionUITransform;
    private void Start()
    {
        if (Camera.main != null)
            mainCameraTransform = Camera.main.transform;
    }

    void Update()
    {
        string rightInteractableObjName = GetRightHoveredObjectName();
        if (!string.IsNullOrEmpty(rightInteractableObjName))
        {
            rightControllerUIText.text = rightInteractableObjName;
        }
        string leftInteractableObjName = GetLeftHoveredObjectName();
        if (!string.IsNullOrEmpty(leftInteractableObjName))
        {
            leftControllerUIText.text = leftInteractableObjName;
        }

        //PositionUI();
    }

    private void PositionUI()
    {
        //if(_leftControllerTransform != null && leftControllerUITranform != null)
        //{
        //    leftControllerUITranform = _leftControllerTransform;
        //    leftControllerUITranform.LookAt(mainCameraTransform);
        //}
        //if(_rightControllerTransform != null && rightControllerUIText != null)
        //{
        //    rightControllerUITransform = _rightControllerTransform;
        //    rightControllerUITransform.LookAt(mainCameraTransform);
        //}

        
    }

    public string GetRightHoveredObjectName()
    {
        if (rightTargetGroup == null) return "No Group";

        // 1. Get the interactor the Group has currently "crowned" as the winner
        var interactor = rightTargetGroup.activeInteractor;
        if (interactor == null) return "Empty";

        // 2. Check if the winner is currently HOLDING something (Selection)
        if (interactor is IXRSelectInteractor selectInteractor)
        {
            if (selectInteractor.interactablesSelected.Count > 0)
            {
                // Returns the name of the grabbed object
                return selectInteractor.interactablesSelected[0].transform.name + " (Grabbed)";
            }
        }

        // 3. If not holding anything, check if they are LOOKING at something (Hover)
        if (interactor is IXRHoverInteractor hoverInteractor)
        {
            if (hoverInteractor.interactablesHovered.Count > 0)
            {
                // Returns the name of the hovered object
                return hoverInteractor.interactablesHovered[0].transform.name + " (Hovering)";
            }
        }

        return "Empty";
    }

    public string GetLeftHoveredObjectName()
    {
        if (leftTargetGroup == null) return "No Group";

        var interactor = leftTargetGroup.activeInteractor;
        if (interactor == null) return "Empty";

        if (interactor is IXRSelectInteractor selectInteractor)
        {
            if (selectInteractor.interactablesSelected.Count > 0)
            {
                return selectInteractor.interactablesSelected[0].transform.name + " (Grabbed)";
            }
        }

        if (interactor is IXRHoverInteractor hoverInteractor)
        {
            if (hoverInteractor.interactablesHovered.Count > 0)
            {
                return hoverInteractor.interactablesHovered[0].transform.name + " (Hovering)";
            }
        }

        return "Empty";
    }
}