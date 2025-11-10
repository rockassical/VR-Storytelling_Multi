using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("Speaking UI")]
    public Button MicButton;
    public Image TimeBar;

    [Header("Text Edit UI")]
    public TMP_InputField UserInput;
    public Button ConfirmButton;
    public Button CancelButton;

    [Header("Response UI")]
    public TMP_Text AIResponseText;

    [Header("Loading UI")]
    public TMP_Text LoadingText;

    private float totalTime = 10f;
    private bool isListening = false;

    void Start()
    {
        // Initial visibility
        ShowIdleState();

        // Button events
        MicButton.onClick.AddListener(StartListening);
        ConfirmButton.onClick.AddListener(OnConfirm);
        CancelButton.onClick.AddListener(ShowIdleState);
    }

    void StartListening()
    {
        if (!isListening)
        {
            isListening = true;
            StartCoroutine(MicTimer());
        }
    }

    IEnumerator MicTimer()
    {
        float timeRemaining = totalTime;
        TimeBar.fillAmount = 1f;
        LoadingText.gameObject.SetActive(false);
        AIResponseText.text = "";

        while (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            TimeBar.fillAmount = timeRemaining / totalTime;
            yield return null;
        }

        isListening = false;
        StartCoroutine(SimulateResponse());
    }

    IEnumerator SimulateResponse()
    {
        LoadingText.gameObject.SetActive(true);
        LoadingText.text = "Thinking...";
        yield return new WaitForSeconds(3f);

        LoadingText.gameObject.SetActive(false);
        AIResponseText.text = "AI: The mitochondrion is the powerhouse of the cell!";
    }

    void OnConfirm()
    {
        string userText = UserInput.text;
        if (!string.IsNullOrEmpty(userText))
        {
            StartCoroutine(SimulateResponse());
        }
    }

    void ShowIdleState()
    {
        LoadingText.gameObject.SetActive(false);
        AIResponseText.text = "";
        TimeBar.fillAmount = 1f;
    }
}

