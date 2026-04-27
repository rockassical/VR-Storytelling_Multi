using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.Netcode; // Added for Netcode

public class MutatedP53 : NetworkBehaviour
{

    /*
    - Mutated p53 enemy that moves toward the floating DNA pieces and tries to steal them
    - Runs away, but can't cause users to fail. Meant to be more of an annoyance than a threat
    */

    public int Health;
    private int MaxHealth;
    public float Speed;

    public GameObject HealthBar;
    public Image HealthBarValue;

    private NetworkVariable<bool> n_hasPiece = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // We use a NetworkVariable for Health so clients see the health bar update
    private NetworkVariable<int> n_currentHealth = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    //private bool hasPiece; //Comment out eventually

    [SerializeField] private GameObject[] DNAPieces;
    [SerializeField] private GameObject heldPiece;
    private Vector3 spawnPos;

    // Start is called before the first frame update
    void Start()
    {
        MaxHealth = Health;
        if (IsServer) n_currentHealth.Value = Health;

        spawnPos = transform.position;
        //hasPiece = false;//Comment out
        
        DNAPieces = GameObject.FindGameObjectsWithTag("HR DNA Piece");
        n_currentHealth.OnValueChanged += (oldVal, newVal) =>
        {
            UpdateHealthUI(newVal);
        };
            
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsServer) return; //Added

        //if(hasPiece == false){ //Commented out
        if (n_hasPiece.Value == false) { 
            // Move towards the closest DNA piece to try and steal it
            GameObject closestPiece = FindClosestPiece();
            //Debug.Log("Moving to " + closestPiece);
            if(closestPiece != null){
                transform.position = Vector3.MoveTowards(transform.position, closestPiece.transform.position, Speed);

                //if(gameObject.transform.position.Equals(closestPiece.transform.position)){ //Commented out
                if (Vector3.Distance(transform.position, closestPiece.transform.position) < 0.1f) { 
                    // Pickup object

                    PickUpPieceServer(closestPiece);
                    //heldPiece = closestPiece; //Commented out

                    //heldPiece.transform.SetParent(gameObject.transform);
                    //heldPiece.transform.localPosition = new Vector3(0f, 0f, 0f);
                    //hasPiece = true;

                    //setXRGrabable(false);
                }
            }
        }
        else if(transform.position != spawnPos)
        {
            transform.position = Vector3.MoveTowards(transform.position, spawnPos, Speed);
        }

        if(heldPiece != null){
            heldPiece.transform.localPosition = Vector3.zero;
        }
    }

    void PickUpPieceServer(GameObject piece) //Added
    {
        heldPiece = piece;

        // Sync the parenting over the network
        NetworkObject pieceNetObj = heldPiece.GetComponent<NetworkObject>();
        if (pieceNetObj != null)
        {
            pieceNetObj.TrySetParent(transform); // NGO way to sync parenting
        }
        else
        {
            heldPiece.transform.SetParent(transform);
        }

        heldPiece.transform.localPosition = Vector3.zero;
        n_hasPiece.Value = true;

        SetXRGrabableClientRpc(false);
    }

    //GameObject FindClosestPiece(){ //Commented out
    //    GameObject Closest = null;

    //    for(int i = 0; i < DNAPieces.Length; i++){

    //        // Check if the parent of the piece is a p53 (if it is already picked up)
    //        // if this piece isn't already stolen
    //        if (DNAPieces[i].tag.Equals("HR DNA Piece"))
    //        {
    //            if (DNAPieces[i].transform.parent != null)
    //            {
    //                if (DNAPieces[i].transform.parent.gameObject.GetComponent<MutatedP53>() == null)
    //                {

    //                    //Debug.Log("Piece isn't stolen");

    //                    // if this is the first piece that isn't stolen
    //                    if (Closest == null)
    //                    {
    //                        Closest = DNAPieces[i];
    //                    }
    //                    else if (Vector3.Distance(gameObject.transform.position, DNAPieces[i].transform.position) <= Vector3.Distance(gameObject.transform.position, Closest.transform.position))
    //                    {
    //                        Closest = DNAPieces[i];
    //                    }
    //                }
    //            }
    //            else
    //            {
    //                // if this is the first piece that isn't stolen
    //                if (Closest == null)
    //                {
    //                    Closest = DNAPieces[i];
    //                }
    //                else if (Vector3.Distance(gameObject.transform.position, DNAPieces[i].transform.position) <= Vector3.Distance(gameObject.transform.position, Closest.transform.position))
    //                {
    //                    Closest = DNAPieces[i];
    //                }
    //            }
    //        }


    //    }

    //    //Debug.Log("Closest piece is " + Closest.ToString());

    //    return Closest;
    //}

    private void UpdateHealthUI(int currentHealth) //Added
    {
        if (HealthBar.activeSelf == false) HealthBar.SetActive(true);
        HealthBarValue.fillAmount = (float)currentHealth / MaxHealth;
    }

    public void OnTriggerEnter(Collider col){
        if (!IsServer) return; //Added

        if (col.gameObject.CompareTag("LaserBullet"))
        {
            n_currentHealth.Value -= 10;
            Destroy(col.gameObject); // Bullet destruction is usually fine locally or via NetworkObject
            CheckDeath();
        }

        //if(col.gameObject.CompareTag("LaserBullet")){ //Commented out
        //    //Debug.Log("I'M HIT! (mutated p53)");
        //    Health -= 10;

        //    Destroy(col.gameObject);

        //    if(HealthBar.activeSelf == false){
        //        HealthBar.SetActive(true);
        //    }

        //    HealthBarValue.fillAmount = (1.0f * Health) / (1.0f * MaxHealth);

        //    CheckDeath();
        //}
    }

    void CheckDeath(){
        if (n_currentHealth.Value <= 0) //Added
        {
            if (heldPiece != null)
            {
                NetworkObject pieceNetObj = heldPiece.GetComponent<NetworkObject>();
                if (pieceNetObj != null) pieceNetObj.TryRemoveParent();
                else heldPiece.transform.SetParent(null);

                SetXRGrabableClientRpc(true);
            }

            // Despawn the enemy across the network
            GetComponent<NetworkObject>().Despawn();
        }

        //if(Health <= 0){ //Commented out
        //    if(heldPiece != null){
        //        heldPiece.transform.SetParent(null);
        //    }

        //    setXRGrabable(true);
        //    Destroy(gameObject);
        //}
    }

    // added
    [ClientRpc]
    void SetXRGrabableClientRpc(bool value)
    {
        if (heldPiece == null) return;
        XRGrabInteractable pieceGrab = heldPiece.GetComponent<XRGrabInteractable>();
        if (pieceGrab != null) pieceGrab.enabled = value;
    }

    // FindClosestPiece logic remains mostly the same, but should only run on Server
    GameObject FindClosestPiece() { /* Your existing logic */ return null; }

    //void setXRGrabable(bool value) //Commented out
    //{
    //    if(heldPiece == null) {
    //        Debug.Log("Broke ur code dummy. Cannot set XR Grabable");
    //        return;
    //    }

    //    XRGrabInteractable piece = heldPiece.GetComponent<XRGrabInteractable>();

    //    if (value)
    //    {
    //        piece.enabled = true;
    //    }
    //    else
    //    {
    //        piece.enabled=false;
    //    }
    //}
}
