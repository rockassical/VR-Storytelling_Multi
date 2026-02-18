using UnityEngine;

public abstract class NHEJPhaseHandler : MonoBehaviour
{
    [HideInInspector] public NHEJManager manager;

    public abstract void Setup();
    public abstract void StartPhase();
    public abstract void UpdatePhase();
    public abstract void CompletePhase();

    public virtual bool IsAutomatic => true;
}
