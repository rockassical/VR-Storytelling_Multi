using System.Collections;
using UnityEngine;
using TMPro;

// Phase 8: Assessment — displays before/after DNA comparison, score, educational summary.
public class Phase8_Assessment : NHEJPhaseHandler
{
    [Header("UI")]
    [SerializeField] GameObject assessmentPanel;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text summaryText;
    [SerializeField] TMP_Text phaseTitle;

    [Header("DNA Comparison")]
    [SerializeField] GameObject originalDNAModel;
    [SerializeField] GameObject repairedDNAModel;

    [Header("Scoring")]
    [SerializeField] float maxScore = 100f;

    [Header("Timing")]
    [SerializeField] float displayDuration = 15f;
    [SerializeField] bool autoAdvance = true;

    Coroutine activeCoroutine;
    float startTime;

    public override bool IsAutomatic => true;

    public override void Setup()
    {
        startTime = Time.time;
    }

    public override void StartPhase()
    {
        float totalTime = Time.time - startTime;

        // Show assessment panel
        if (assessmentPanel != null)
            assessmentPanel.SetActive(true);

        if (phaseTitle != null)
            phaseTitle.text = "NHEJ Repair Complete";

        // Calculate a simple score based on total repair time
        float score = Mathf.Clamp(maxScore - (totalTime * 0.5f), 10f, maxScore);

        if (scoreText != null)
            scoreText.text = $"Score: {score:F0} / {maxScore:F0}\nTime: {totalTime:F1}s";

        if (summaryText != null)
        {
            summaryText.text =
                "Non-Homologous End Joining (NHEJ) is the primary pathway for repairing " +
                "DNA double-strand breaks in human cells.\n\n" +
                "Key steps completed:\n" +
                "1. Ku70/80 recognized and bound the broken ends\n" +
                "2. DNA-PKcs docked and autophosphorylated\n" +
                "3. Artemis trimmed incompatible overhangs\n" +
                "4. XRCC4/XLF scaffold aligned the ends\n" +
                "5. DNA Ligase IV sealed the nicks\n" +
                "6. VCP/p97 removed Ku for final cleanup\n\n" +
                "NHEJ is fast but error-prone — small insertions or deletions " +
                "may occur at the repair site.";
        }

        // Show DNA comparison
        if (originalDNAModel != null) originalDNAModel.SetActive(true);
        if (repairedDNAModel != null) repairedDNAModel.SetActive(true);

        // Play Alysia summary dialogue
        if (NHEJAudio.Instance != null)
        {
            float clipLen = NHEJAudio.Instance.PlayAlysiaDialogue(8);
            if (clipLen > displayDuration) displayDuration = clipLen + 2f;
        }

        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayPhaseComplete();

        if (autoAdvance)
            activeCoroutine = StartCoroutine(WaitAndAdvance());
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        if (assessmentPanel != null)
            assessmentPanel.SetActive(false);
    }

    IEnumerator WaitAndAdvance()
    {
        yield return new WaitForSeconds(displayDuration);

        if (manager != null && manager.IsServer)
        {
            manager.AdvancePhase();
        }
    }
}
