using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class InteractableObjectsUI : MonoBehaviour
{
    public XRRayInteractor _leftInteractor, _rightInteractor;

    private void OnEnable()
    {
        // In XRI 3.x, we subscribe to the interactor's hoverEvents
        if (_leftInteractor != null)
        {
            _leftInteractor.hoverEntered.AddListener(OnLeftHoverEnter);
            _leftInteractor.hoverExited.AddListener(OnLeftHoverExit);
        }

        if (_rightInteractor != null)
        {
            _rightInteractor.hoverEntered.AddListener(OnRightHoverEnter);
            _rightInteractor.hoverExited.AddListener(OnRightHoverExit);
        }
    }

    private void OnDisable()
    {
        if (_leftInteractor != null)
        {
            _leftInteractor.hoverEntered.RemoveListener(OnLeftHoverEnter);
            _leftInteractor.hoverExited.RemoveListener(OnLeftHoverExit);
        }

        if (_rightInteractor != null)
        {
            _rightInteractor.hoverEntered.RemoveListener(OnRightHoverEnter);
            _rightInteractor.hoverExited.RemoveListener(OnRightHoverExit);
        }
    }

    // Event handlers for the Left Hand
    private void OnLeftHoverEnter(HoverEnterEventArgs args)
    {
        Debug.Log($"<color=cyan>Left Hand</color> hovering over: {args.interactableObject.transform.name}");
    }

    private void OnLeftHoverExit(HoverExitEventArgs args)
    {
        Debug.Log("<color=cyan>Left Hand</color> stopped hovering.");
    }

    // Event handlers for the Right Hand
    private void OnRightHoverEnter(HoverEnterEventArgs args)
    {
        Debug.Log($"<color=yellow>Right Hand</color> hovering over: {args.interactableObject.transform.name}");
    }

    private void OnRightHoverExit(HoverExitEventArgs args)
    {
        Debug.Log("<color=yellow>Right Hand</color> stopped hovering.");
    }
}