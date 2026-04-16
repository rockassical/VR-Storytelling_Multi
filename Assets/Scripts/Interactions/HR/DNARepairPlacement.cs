using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.Playables;

public class DNARepairPlacement : MonoBehaviour
{
    [Header("NEXT TIMELINE")]
    public PlayableDirector Timeline_NHEJ;

    [Header("GAME MANAGER")]
    public GameManager GM;

    /*
        - Declare the number of pieces that need to be placed for strand to be repaired
        - Check to see if strand has been repaired
        - Hide hologram when repaired
    */

    [Header("REPAIR SOCKETS")]
    public GameObject[] Sockets;
    private bool[] IsSocketFilled;

    private bool isRepaired;

    // Start is called before the first frame update
    void Start()
    {
        isRepaired = false;
        IsSocketFilled = new bool[Sockets.Length];

        for(int i = 0; i < Sockets.Length; i++){
            int index = i;
            Sockets[i].GetComponent<XRSocketInteractor>().selectEntered.AddListener((SelectEnterEventArgs args) => FillSocket(index, args));
        }
    }

   // Process the socket whenever it is filled --> stop from grabbing piece and hide socket
   public void FillSocket(int index, SelectEnterEventArgs args){
        IsSocketFilled[index] = true;
        
        var Socket = Sockets[index];
        var Piece = args.interactableObject.transform.gameObject;

        StartCoroutine(WaitAndFillSocket(Socket, Piece));
   }

   // Wait for the piece to click into place
   IEnumerator WaitAndFillSocket(GameObject Socket, GameObject Piece){
       yield return new WaitForSeconds(0.5f);

       Socket.SetActive(false);
       Piece.GetComponent<XRGrabInteractable>().enabled = false;

       CheckIfSocketsAreFilled();
   }

   void CheckIfSocketsAreFilled(){
       bool AllFilled = true;

       for(int i = 0; i < IsSocketFilled.Length; i++){
           if(!IsSocketFilled[i]){
               AllFilled = false;
           }
       }

       if(AllFilled){
           Debug.Log("ALL SOCKETS ARE FILLED! TIME FOR NHEJ!");
            // ACTIVATE TIMELINE --> MOVE TO NEXT PART
            //Timeline_NHEJ.Play();

            GM.playPhase(2); 
            
       }
   }
}
