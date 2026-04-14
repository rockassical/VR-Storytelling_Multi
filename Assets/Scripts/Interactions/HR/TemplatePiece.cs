using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TemplatePiece : MonoBehaviour
{
    public Material UnscannedMat;
    public Material ScannedMatOutsideBars;
    public Material ScannedMatInsideBars;

    public GameObject OutsidePiece;
    public GameObject InsidePiece;

    public bool scanned = false;

    // Start is called before the first frame update
    void Start()
    {
        OutsidePiece.GetComponent<MeshRenderer>().material = UnscannedMat;
        InsidePiece.GetComponent<MeshRenderer>().material = UnscannedMat;
    }

    public void ScanPiece(){
        scanned = true;
        OutsidePiece.GetComponent<MeshRenderer>().material = ScannedMatOutsideBars;
        InsidePiece.GetComponent<MeshRenderer>().material = ScannedMatInsideBars;
    }
}
