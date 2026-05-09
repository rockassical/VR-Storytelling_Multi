using UnityEngine;
using UnityEngine.XR;

// Add this component anywhere in the scene and drag the XR Origin transform into the field.
// Right thumbstick left/right snaps the player's view by snapAngle degrees.
// Set turnMode to Continuous for smooth rotation instead.
public class VRSnapTurn : MonoBehaviour
{
    public enum TurnMode { Snap, Continuous }

    [SerializeField] TurnMode turnMode = TurnMode.Snap;
    [SerializeField] float snapAngle = 45f;
    [SerializeField] float continuousSpeed = 90f;   // degrees per second
    [SerializeField] float deadzone = 0.5f;
    [Tooltip("The XR Origin transform to rotate (drag it here from the hierarchy).")]
    [SerializeField] Transform xrOriginTransform;

    bool snapReady = true;

    void Update()
    {
        InputDevices.GetDeviceAtXRNode(XRNode.RightHand)
            .TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis);

        float x = axis.x;

        if (turnMode == TurnMode.Snap)
        {
            if (Mathf.Abs(x) > deadzone && snapReady)
            {
                Rotate(x > 0 ? snapAngle : -snapAngle);
                snapReady = false;
            }
            else if (Mathf.Abs(x) < deadzone * 0.4f)
            {
                snapReady = true;
            }
        }
        else
        {
            if (Mathf.Abs(x) > deadzone)
                Rotate(x * continuousSpeed * Time.deltaTime);
        }
    }

    void Rotate(float angle)
    {
        if (xrOriginTransform == null) return;

        // Rotate around the camera's feet so the view stays centred on the player.
        Transform cam = Camera.main != null ? Camera.main.transform : xrOriginTransform;
        Vector3 pivot = new Vector3(cam.position.x, xrOriginTransform.position.y, cam.position.z);
        xrOriginTransform.RotateAround(pivot, Vector3.up, angle);
    }
}
