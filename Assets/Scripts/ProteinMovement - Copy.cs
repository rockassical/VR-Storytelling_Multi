using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//controls proteins spawned by the DNARepair script and moves them
//toward damage site on DNA strand (empty object)

public class ProteinMovement : MonoBehaviour
{
    private GameObject target;

    void Start(){
        target = GameObject.FindWithTag("Hole");
        Debug.Log(target.ToString());
    }

    void Update(){
        transform.position = Vector3.MoveTowards(transform.position, target.transform.position, 0.01f);
    }
}
