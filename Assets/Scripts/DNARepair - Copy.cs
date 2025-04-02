using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DNARepair : MonoBehaviour
{
    //Detect for hits on this (hole object)
    //On hits, spawn proteins at one random location, and they move toward break site
    //go from there (either they instantly repair a bit or do something particular)



    //list of points to spawn proteins from
    public Transform[] spawnPoints;
    public GameObject protein;
    private int numSpawned;

    void Start(){
        numSpawned = 0;
        GetComponent<BoxCollider>().enabled = true;
        Debug.Log(numSpawned);
    }

    void OnParticleCollision(GameObject col){
        Debug.Log("Collision");
        if(numSpawned < 5){
            Instantiate(protein, spawnPoints[Random.Range(0, spawnPoints.Length)]);
            numSpawned++;
        }
    }
}
