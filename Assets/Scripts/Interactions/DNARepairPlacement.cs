using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DNARepairPlacement : MonoBehaviour
{

    /*
        - Declare the number of pieces that need to be placed for strand to be repaired
        - Check to see if strand has been repaired
        - Replace strand when repaired
    */

    [Header("DNA STRAND OBJECTS")]
    public GameObject DamagedStrand;
    public GameObject RepairedStrand;

    [Header("REPAIR SOCKETS")]
    public GameObject[] Sockets;
    private bool[] IsSocketFilled;

    private bool isRepaired;

    // Start is called before the first frame update
    void Start()
    {
        isRepaired = false;
        IsSocketFilled = new bool[Sockets.Length];
    }

    // Update is called once per frame
    void Update()
    {
        isRepaired = true;
        for(int i = 0; i < Sockets.Length; i++){
            if(!IsSocketFilled[i]){
                isRepaired = false;
            }
        }

        if(isRepaired){
            DamagedStrand.SetActive(false);
            RepairedStrand.SetActive(true);
            for(int i = 0; i < Sockets.Length; i++){
                Sockets[i].SetActive(false);
            }
        }
    }

    public void FillSocket(GameObject Socket){
        for(int i = 0; i < Sockets.Length; i++){
            if(Sockets[i].Equals(Socket)){
                IsSocketFilled[i] = true;
            }
        }
    }
}
