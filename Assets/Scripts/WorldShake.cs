using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldShake : MonoBehaviour
{
    public GameObject environment;
    public float magnitude;

    [SerializeField] private float time;

    public bool activate;
    
    void Update(){
        if(activate){
            Debug.Log("Shaking");
            StartCoroutine(ShakeEnvironment(environment.transform, time));
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
