using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;

public class GunInputXR : MonoBehaviour
{
    [SerializeField] public GameObject ScanPulse;
    [SerializeField] XRGrabInteractable grab;

    [Header("Input")]
    [SerializeField] InputActionProperty scanAction;

    [Header("Socket Interaction Objects")]
    public GameObject[] Sockets;

    public int ScannedPieces;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        ScannedPieces = 0;
    }

    void OnEnable()
    {
        scanAction.action.Enable();
    }

    void OnDisable()
    {
        scanAction.action.Disable();
    }

    void Update()
    {
        if (!grab.isSelected)
            return;

        if (scanAction.action.WasPressedThisFrame())
        {
            Scan();
        }
        else if(scanAction.action.WasReleasedThisFrame())
        {
            UnScan();
        }
    }


    void Scan(){
        ScanPulse.SetActive(true);
    }

    void UnScan(){
        ScanPulse.SetActive(false);
    }

    public void OnTriggerEnter(Collider col){
        if(col.gameObject.tag.Equals("Scannable")){

            var template = col.gameObject.GetComponent<TemplatePiece>();

            // Make sure piece isn't rescanned
            if(!template.scanned){

                Debug.Log("TEMPLATE PIECE SCANNED");

                ScannedPieces++;
                template.ScanPiece();

                // if all pieces have been scanned, activate holograms
                if(ScannedPieces == Sockets.Length){

                    foreach(GameObject Socket in Sockets){
                        Socket.SetActive(true);
                    }

                }
            }
        }
    }

}
