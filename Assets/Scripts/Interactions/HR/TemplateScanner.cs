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

    public static int ScannedPieces = 0;

    [Header("Objects for post-scan")]
    public GameManager gameManager;
    public MutatedProteinSpawner spawner;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        gameManager = GameObject.FindGameObjectsWithTag("Game Manager")[0].GetComponent<GameManager>();
        spawner = GameObject.FindGameObjectsWithTag("Game Manager")[0].GetComponent<MutatedProteinSpawner>();
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

            // Netcode addition
            if(!template.IsScanned){

                Debug.Log("TEMPLATE PIECE SCANNED");

                ScannedPieces++;
                // ScanPiece() now routes through a ServerRpc so all clients see the material change
                template.ScanPiece();
                if(ScannedPieces >= Sockets.Length){

                    gameManager.playPhase(2);
                    spawner.isActive = true;

                    foreach(GameObject Socket in Sockets){
                        Socket.SetActive(true);
                    }

                }
            }
        }
    }

}
