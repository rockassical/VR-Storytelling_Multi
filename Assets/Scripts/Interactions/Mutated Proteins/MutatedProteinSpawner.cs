using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class MutatedProteinSpawner : NetworkBehaviour
{

    public GameObject[] spawnPositions;

    public GameObject mutatedP53;

    public List<GameObject> SpawnedProteins;

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
        SpawnP53ServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void SpawnP53ServerRpc()
    {
        Vector3 minCorner = spawnPositions[0].transform.position;
        Vector3 maxCorner = spawnPositions[1].transform.position;

        Vector3 RandomPosition = new Vector3(Random.Range(minCorner.x, maxCorner.x), Random.Range(minCorner.y, maxCorner.y), Random.Range(minCorner.z, maxCorner.z));

        if(mutatedP53 == null) return;
        var go = Instantiate(mutatedP53, RandomPosition, Quaternion.identity);
        SpawnedProteins.Add(go);
        var no = go.GetComponent<NetworkObject>();
        if (no != null) no.Spawn();
    }

    public void DespawnAll(){
        for(int i = 0; i < SpawnedProteins.Count; i++){
            if(SpawnedProteins[i] != null){
                Destroy(SpawnedProteins[i]);
            }
        }
    }
}
