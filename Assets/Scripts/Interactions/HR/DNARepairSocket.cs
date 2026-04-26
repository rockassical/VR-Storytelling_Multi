using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DNARepairSocket : XRSocketInteractor
{
    [Header("Which Piece Goes Here?")]
    public int SocketNumber;

    // Captured before XRI reparents the piece into the socket so we can preserve
    // the piece's actual visual size regardless of the socket's parent chain.
    // This fixes the case where P2 sees socketed pieces as giant because some
    // ancestor of the socket on P2's side has a non-1 lossyScale.
    Vector3 _capturedWorldScale = Vector3.one;
    bool _hasCapture;
    Transform _capturedTransform;

    public override bool CanHover(IXRHoverInteractable interactable)
    {
        if (!base.CanHover(interactable))
            return false;

        var obj = interactable;
        return obj.transform.gameObject.ToString().Equals("P" + SocketNumber + " (UnityEngine.GameObject)");
    }

    // Overrides selection to only allow the correct DNA piece to fill this socket
    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        var obj = interactable.transform.gameObject;
        if (obj == null)
            return false;

        return obj.transform.gameObject.ToString().Equals("P" + SocketNumber + " (UnityEngine.GameObject)");
    }

    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        var t = args.interactableObject.transform;

        // Prefer the prefab's rest scale (captured at Awake before any grab/parent
        // could warp it). Fall back to current lossyScale only if no rest-scale
        // component is present.
        var rest = t.GetComponent<DNAPieceRestScale>();
        _capturedWorldScale = rest != null ? rest.RestScale : t.lossyScale;
        _capturedTransform  = t;
        _hasCapture         = true;

        base.OnSelectEntering(args);
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        if (!_hasCapture || _capturedTransform == null) return;
        ApplyCapturedScale();
    }

    void ApplyCapturedScale()
    {
        Vector3 parentLossy = _capturedTransform.parent != null
            ? _capturedTransform.parent.lossyScale
            : Vector3.one;

        _capturedTransform.localScale = new Vector3(
            _capturedWorldScale.x / Mathf.Max(Mathf.Abs(parentLossy.x), 0.0001f),
            _capturedWorldScale.y / Mathf.Max(Mathf.Abs(parentLossy.y), 0.0001f),
            _capturedWorldScale.z / Mathf.Max(Mathf.Abs(parentLossy.z), 0.0001f)
        );
    }

    void LateUpdate()
    {
        // Re-enforce the desired scale on the socketed piece every frame. This
        // catches the case where another component (XRI internals, OwnerTransformSync,
        // etc.) writes localScale after our OnSelectEntered ran.
        if (_hasCapture && _capturedTransform != null && hasSelection)
            ApplyCapturedScale();

        Debug.Log("Parent scale: " + _capturedTransform.parent.lossyScale);
        Debug.Log("Applied local scale: " + _capturedTransform.localScale);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        _hasCapture = false;
        _capturedTransform = null;
    }
}
