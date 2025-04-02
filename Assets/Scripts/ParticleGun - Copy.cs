using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Transformers;


public class ParticleGun : MonoBehaviour
{
    public GameObject lever;
    public GameObject particle;

    private ParticleSystem parts;
    
    void Awake(){
        parts = particle.GetComponent<ParticleSystem>();
        parts.Stop();
        parts.Simulate(0f, true, true);
        parts.Clear();
        parts.Clear();
    }

    void Update(){
        if(lever.transform.localRotation.eulerAngles.x <= 40f){
            parts.Play();
        }else{
            parts.Stop();
        }
    }
}
