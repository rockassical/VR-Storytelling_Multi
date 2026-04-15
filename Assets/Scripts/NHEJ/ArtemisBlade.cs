using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach to a CHILD GameObject of the Artemis prefab that has a small trigger Collider (the blade tip).
// When the player is holding Artemis and swings this collider into an OverhangZone,
// it fires a cut via NHEJManager.ReportCutServerRpc.
//
// Prefab setup:
//   - On Artemis root: ArtemisOrbitController, XRGrabInteractable, NHEJTool, NetworkObject, NetworkTransform
//   - Add a child "BladeTip" with a small CapsuleCollider (IsTrigger = true) and this component.
//   - Orient/position the collider at the cutting edge of the Artemis mesh.
[RequireComponent(typeof(Collider))]
public class ArtemisBlade : MonoBehaviour
{
    [Header("Cut Settings")]
    [Tooltip("Minimum blade tip speed (m/s) required to trigger a cut. Prevents accidental cuts while barely touching.")]
    [SerializeField] float minimumSwingSpeed = 0.3f;

    XRGrabInteractable parentGrab;
    NetworkObject parentNetworkObject;
    bool isHeld;
    bool hasCutThisGrab;    // one cut per grab to prevent spamming

    Vector3 prevPosition;
    float   bladeSpeed;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning("[ArtemisBlade] Collider was not a trigger — fixed automatically.");
        }
    }

    void Start()
    {
        // Walk up to the Artemis root to find the grab interactable and NetworkObject.
        parentGrab          = GetComponentInParent<XRGrabInteractable>();
        parentNetworkObject = GetComponentInParent<NetworkObject>();

        if (parentGrab != null)
        {
            parentGrab.selectEntered.AddListener(OnParentGrabbed);
            parentGrab.selectExited.AddListener(OnParentReleased);
        }

        prevPosition = transform.position;
    }

    void OnDestroy()
    {
        if (parentGrab != null)
        {
            parentGrab.selectEntered.RemoveListener(OnParentGrabbed);
            parentGrab.selectExited.RemoveListener(OnParentReleased);
        }
    }

    void OnParentGrabbed(SelectEnterEventArgs _)
    {
        isHeld            = true;
        hasCutThisGrab    = false;
        prevPosition      = transform.position;
    }

    void OnParentReleased(SelectExitEventArgs _)
    {
        isHeld         = false;
        hasCutThisGrab = false;
    }

    void Update()
    {
        // Track blade tip speed for minimum-swing check.
        bladeSpeed   = Vector3.Distance(transform.position, prevPosition) / Time.deltaTime;
        prevPosition = transform.position;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isHeld || hasCutThisGrab) return;
        if (bladeSpeed < minimumSwingSpeed) return;

        // Only the owner of the Artemis NetworkObject fires the RPC.
        // (The owner is the client currently holding it.)
        if (parentNetworkObject == null || !parentNetworkObject.IsOwner) return;

        var zone = other.GetComponent<OverhangZone>();
        if (zone == null) return;

        hasCutThisGrab = true;

        if (NHEJManager.Instance != null)
        {
            NHEJManager.Instance.ReportCutServerRpc(
                transform.position,
                NetworkManager.Singleton.LocalClientId,
                zone.playerRole);
        }
    }
}
