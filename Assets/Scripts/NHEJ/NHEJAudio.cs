using UnityEngine;

// audio manager for NHEJ phase transitions and interactions
public class NHEJAudio : MonoBehaviour
{
    public static NHEJAudio Instance { get; private set; }

    [Header("Phase Transition Sounds")]
    [SerializeField] AudioClip phaseAdvanceClip;
    [SerializeField] AudioClip phaseCompleteClip;

    [Header("Interaction Sounds")]
    [SerializeField] AudioClip trimSuccessClip;
    [SerializeField] AudioClip trimFailClip;
    [SerializeField] AudioClip ligationSuccessClip;
    [SerializeField] AudioClip ligationFailClip;
    [SerializeField] AudioClip snapClip;
    [SerializeField] AudioClip glowClip;

    [Header("Alysia Dialogue (assign when available)")]
    [SerializeField] AudioClip[] alysiaPhaseDialogue;

    [Header("Settings")]
    [SerializeField] AudioSource sfxSource;
    [SerializeField] AudioSource dialogueSource;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlayPhaseAdvance()
    {
        PlaySFX(phaseAdvanceClip);
    }

    public void PlayPhaseComplete()
    {
        PlaySFX(phaseCompleteClip);
    }

    public void PlayTrimSuccess()
    {
        PlaySFX(trimSuccessClip);
    }

    public void PlayTrimFail()
    {
        PlaySFX(trimFailClip);
    }

    public void PlayLigationSuccess()
    {
        PlaySFX(ligationSuccessClip);
    }

    public void PlayLigationFail()
    {
        PlaySFX(ligationFailClip);
    }

    public void PlaySnap()
    {
        PlaySFX(snapClip);
    }

    public void PlayGlow()
    {
        PlaySFX(glowClip);
    }

    public float PlayAlysiaDialogue(int phaseIndex)
    {
        if (alysiaPhaseDialogue == null || phaseIndex < 0 || phaseIndex >= alysiaPhaseDialogue.Length)
            return 0f;

        var clip = alysiaPhaseDialogue[phaseIndex];
        if (clip == null) return 0f;

        if (dialogueSource != null)
        {
            dialogueSource.clip = clip;
            dialogueSource.Play();
            return clip.length;
        }
        return 0f;
    }

    void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip);
    }
}
