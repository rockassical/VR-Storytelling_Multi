using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
