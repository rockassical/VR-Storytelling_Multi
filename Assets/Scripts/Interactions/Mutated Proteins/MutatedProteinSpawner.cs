using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MutatedProteinSpawner : MonoBehaviour
{

    public GameObject[] spawnPositions;

    public GameObject mutatedP53;

    public bool isActive;

    [Header("1/[Chance] to spawn every frame (500-600 base)")]
    public int Chance;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(isActive){
            if(Random.Range(0, Chance) == 154){
                SpawnP53();
            }
        }
    }

    void SpawnP53(){
        Vector3 minCorner = spawnPositions[0].transform.position;
        Vector3 maxCorner = spawnPositions[1].transform.position;

        Vector3 RandomPosition = new Vector3(Random.Range(minCorner.x, maxCorner.x), Random.Range(minCorner.y, maxCorner.y), Random.Range(minCorner.z, maxCorner.z));

        Instantiate(mutatedP53, RandomPosition, Quaternion.identity);
    }
}
