using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DNARepair : MonoBehaviour
{
    //Detect for hits on this (hole object)
    //On hits, spawn proteins at one random location, and they move toward break site
    //go from there (either they instantly repair a bit or do something particular)

    public Transform[] spawnPoints;   //list of points to spawn proteins from
    public List<GameObject> spawnedProteins;
    public GameObject protein;
    private int numSpawned;
    private int numReached;   //number that reach damage site
    public float spawnCooldown;

    //DNA Strands
    public GameObject broken;
    public GameObject repaired;

    void Start(){
        spawnedProteins = new List<GameObject>();
        numReached = 0;
        numSpawned = 0;
        GetComponent<BoxCollider>().enabled = true;
        Debug.Log(numSpawned);
    }

    void Update(){
        if(spawnCooldown > 0){
            spawnCooldown -= Time.deltaTime;
        }
    }

    void OnParticleCollision(GameObject col){
        Debug.Log("Collision");
        if(numSpawned < 5 && spawnCooldown <= 0){
            spawnedProteins.Add(Instantiate(protein, spawnPoints[Random.Range(0, spawnPoints.Length)]));
            numSpawned++;
            spawnCooldown = 2f;
        }
    }

    void OnTriggerEnter(Collider col){
        if(col.gameObject.tag.Equals("Protein")){
            numReached++;
            Debug.Log(numReached);
            if(numReached >= 5){
                //Despawn proteins
                for(int i = 0; i < spawnedProteins.Count; i++){
                    Destroy(spawnedProteins[i]);
                }
                //make DNA repaired
                broken.SetActive(false);
                repaired.SetActive(true);
            }
        }
    }
}
