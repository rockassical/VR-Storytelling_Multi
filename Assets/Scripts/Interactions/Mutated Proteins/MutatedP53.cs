using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    private GameObject[] DNAPieces;

    // Start is called before the first frame update
    void Start()
    {
        MaxHealth = Health;

        DNAPieces = GameObject.FindGameObjectsWithTag("DNA Piece");
    }

    // Update is called once per frame
    void Update()
    {
        // Move towards the closest DNA piece to try and steal it
        GameObject closestPiece = FindClosestPiece();
        Vector3.MoveTowards(gameObject.transform.position, closestPiece.transform.position, Speed);
    }

    GameObject FindClosestPiece(){
        GameObject Closest = DNAPieces[0];

        for(int i = 0; i < DNAPieces.Length; i++){
            if(Vector3.Distance(gameObject.transform.position, DNAPieces[i].transform.position) <= Vector3.Distance(gameObject.transform.position, Closest.transform.position)){
                Closest = DNAPieces[i];
            }
        }

        return Closest;
    }

    public void OnCollisionEnter(Collision col){
        if(col.gameObject.tag.Equals("Laser")){
            Health -= 10;

            if(HealthBar.activeSelf == false){
                HealthBar.SetActive(true);
            }

            HealthBarValue.fillAmount = Health / MaxHealth;
        }
    }
}
