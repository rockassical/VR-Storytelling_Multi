using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class DNARepairSocket : UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor
{

    [Header("Which Piece Goes Here?")]
    public int SocketNumber;

     public override bool CanHover(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRHoverInteractable interactable)
    {
        if (!base.CanHover(interactable))
            return false;

        var obj = interactable;
        return obj.transform.gameObject.ToString().Equals("P" + SocketNumber + " (UnityEngine.GameObject)");
    }

    // Overrides selection to only allow the correct DNA piece to fill this socket
    public override bool CanSelect(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable)
    {
        var obj = interactable.transform.gameObject;

        if (obj == null)
            return false;

        //Debug.Log(obj.transform.gameObject);

        // Check the name of the gameObject to check if it's the correct one
        return obj.transform.gameObject.ToString().Equals("P" + SocketNumber + " (UnityEngine.GameObject)");
    }
}
