using UnityEngine;

public abstract class NHEJPhaseHandler : MonoBehaviour
{
    [HideInInspector] public NHEJManager manager;

    public abstract void Setup();
    public abstract void StartPhase();
    public abstract void UpdatePhase();
    public abstract void CompletePhase();

    public virtual bool IsAutomatic => true;

    /// <summary>
    /// Called SERVER-ONLY when a player places their protein pickup during this phase.
    /// Default: immediately marks that player complete (phases 0/1/2/3/6 use this).
    /// Override in phases that need post-placement animations before advancing (5, 7).
    /// </summary>
    public virtual void OnProteinPlaced(int playerRole)
    {
        manager.ServerMarkPlayerComplete(playerRole);
    }

    /// <summary>
    /// Called on ALL CLIENTS (via ClientRpc) when a protein placement is confirmed.
    /// Use this to drive local visual/audio responses that must run everywhere.
    /// </summary>
    public virtual void OnProteinPlacedLocal(int playerRole) { }
}
