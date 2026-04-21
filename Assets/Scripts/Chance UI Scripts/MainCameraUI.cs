/*using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditorInternal;
using UnityEngine;

public class MainCameraUI : MonoBehaviour
{
    Camera cam;

    public float maxDistance;
    public TextMeshProUGUI objectText;
    void Start()
    {
       cam = GetComponent<Camera>();
       objectText.gameObject.SetActive(false);
    }
    void FixedUpdate()
    {
        RaycastHit hit;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.red);

        if (Physics.Raycast(ray, out hit, maxDistance))
        {
            if (hit.transform.gameObject.CompareTag("Protein"))
            {
                

                //get pos of hit, float name text above hit
                string textName = hit.transform.gameObject.name;
                Vector3 textPos = hit.transform.position;

                objectText.text = textName;
                objectText.transform.position = textPos;
                
                objectText.gameObject.SetActive(true);
            }
        }
        else
        {
            objectText.gameObject.SetActive(false);
        }
    }
}*/
