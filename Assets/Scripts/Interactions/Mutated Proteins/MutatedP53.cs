using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class MutatedP53 : MonoBehaviour
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

    private bool hasPiece;

    [SerializeField] private GameObject[] DNAPieces;
    [SerializeField] private GameObject heldPiece;
    private Vector3 spawnPos;

    // Start is called before the first frame update
    void Start()
    {
        MaxHealth = Health;
        spawnPos = transform.position;
        hasPiece = false;

        DNAPieces = GameObject.FindGameObjectsWithTag("HR DNA Piece");
    }

    // Update is called once per frame
    void Update()
    {
        if(hasPiece == false){
            // Move towards the closest DNA piece to try and steal it
            GameObject closestPiece = FindClosestPiece();
            //Debug.Log("Moving to " + closestPiece);
            if(closestPiece != null){
                transform.position = Vector3.MoveTowards(transform.position, closestPiece.transform.position, Speed);

                if(gameObject.transform.position.Equals(closestPiece.transform.position)){
                    // Pickup object
                    heldPiece = closestPiece;

                    heldPiece.transform.SetParent(gameObject.transform);
                    heldPiece.transform.localPosition = new Vector3(0f, 0f, 0f);
                    hasPiece = true;

                    setXRGrabable(false);
                }
            }
        }
        else if(transform.position != spawnPos)
        {
            transform.position = Vector3.MoveTowards(transform.position, spawnPos, Speed);
        }

        if(heldPiece != null){
            heldPiece.transform.localPosition = new Vector3(0f, 0f, 0f);
        }
    }

    GameObject FindClosestPiece(){
        GameObject Closest = null;

        for(int i = 0; i < DNAPieces.Length; i++){

            // Check if the parent of the piece is a p53 (if it is already picked up)
            // if this piece isn't already stolen
            if (DNAPieces[i].tag.Equals("HR DNA Piece"))
            {
                if (DNAPieces[i].transform.parent != null)
                {
                    if (DNAPieces[i].transform.parent.gameObject.GetComponent<MutatedP53>() == null)
                    {

                        //Debug.Log("Piece isn't stolen");

                        // if this is the first piece that isn't stolen
                        if (Closest == null)
                        {
                            Closest = DNAPieces[i];
                        }
                        else if (Vector3.Distance(gameObject.transform.position, DNAPieces[i].transform.position) <= Vector3.Distance(gameObject.transform.position, Closest.transform.position))
                        {
                            Closest = DNAPieces[i];
                        }
                    }
                }
                else
                {
                    // if this is the first piece that isn't stolen
                    if (Closest == null)
                    {
                        Closest = DNAPieces[i];
                    }
                    else if (Vector3.Distance(gameObject.transform.position, DNAPieces[i].transform.position) <= Vector3.Distance(gameObject.transform.position, Closest.transform.position))
                    {
                        Closest = DNAPieces[i];
                    }
                }
            }
            
                
        }

        //Debug.Log("Closest piece is " + Closest.ToString());

        return Closest;
    }

    public void OnTriggerEnter(Collider col){
        if(col.gameObject.CompareTag("LaserBullet")){
            //Debug.Log("I'M HIT! (mutated p53)");
            Health -= 10;

            Destroy(col.gameObject);

            if(HealthBar.activeSelf == false){
                HealthBar.SetActive(true);
            }

            HealthBarValue.fillAmount = (1.0f * Health) / (1.0f * MaxHealth);

            CheckDeath();
        }
    }

    void CheckDeath(){
        if(Health <= 0){
            if(heldPiece != null){
                heldPiece.transform.SetParent(null);
            }

            setXRGrabable(true);
            Destroy(gameObject);
        }
    }

    void setXRGrabable(bool value)
    {
        if(heldPiece == null) {
            Debug.Log("Broke ur code dummy. Cannot set XR Grabable");
            return;
        }

        XRGrabInteractable piece = heldPiece.GetComponent<XRGrabInteractable>();

        if (value)
        {
            piece.enabled = true;
        }
        else
        {
            piece.enabled=false;
        }
    }
}
