using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MutatedProteinSpawner : MonoBehaviour
{

    public GameObject[] spawnPositions;

    public GameObject mutatedP53;

    public bool isActive;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(isActive){
            if(Random.Range(0, 750) == 154){
                SpawnP53();
            }
        }
    }

    void SpawnP53(){
        Instantiate(mutatedP53, spawnPositions[Random.Range(0, spawnPositions.Length - 1)].transform.position, Quaternion.identity);
    }
}
