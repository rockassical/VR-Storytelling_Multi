using System.Collections;
using UnityEngine;

// Phase 0: Alysia intro dialogue. Auto-advances after the dialogue clip finishes
// (or after fallbackDuration if no clip is assigned).
public class Phase0_Trigger : NHEJPhaseHandler
{
    [Header("Dialogue")]
    [SerializeField] AudioClip alysiaIntroClip;
    [SerializeField] float fallbackDuration = 5f;

    Coroutine activeCoroutine;

    public override bool IsAutomatic => true;

    public override void Setup() { }

    public override void StartPhase()
    {
        float duration = fallbackDuration;

        if (NHEJAudio.Instance != null)
        {
            float clipLength = NHEJAudio.Instance.PlayAlysiaDialogue(0);
            if (clipLength > 0f) duration = clipLength;
            NHEJAudio.Instance.PlayPhaseAdvance();
        }

        activeCoroutine = StartCoroutine(WaitAndAdvance(duration));
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
    }

    IEnumerator WaitAndAdvance(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (manager != null && manager.IsServer)
            manager.AdvancePhase();
    }
}
