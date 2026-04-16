using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldShake : MonoBehaviour
{
    public GameObject environment;
    public float magnitude;

    [SerializeField] private float timer;

    void Start(){
        timer = 10f;
    }
    
    void Update(){
        if(timer <= 0){
            Debug.Log("Shaking");
            StartCoroutine(ShakeEnvironment(environment.transform, 3f));
            timer = 10f;
        }else{
            timer -= Time.deltaTime;
        }
    }

    IEnumerator ShakeEnvironment(Transform target, float duration)
    {
        Vector3 originalPos = target.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            target.localPosition = originalPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;

            yield return null;
        }

        target.localPosition = originalPos;
    }

}
