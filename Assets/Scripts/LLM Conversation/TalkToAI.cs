using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using UnityEngine.InputSystem;
using OpenAI_API.Audio;
using System.IO;
using System.Threading.Tasks;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class TalkToAI : NetworkBehaviour
{
    
    [SerializeField] private AudioClip MicClip;
    [SerializeField] private string SpeechToText;

    public GameObject Camera;

    //float timer = 10f;
    //bool timerFinished = false;
    private bool recording;

    public AudioSource audioSource;
    private AudioClip audioClip;

    private OpenAIAPI api;

    public InputActionProperty triggerAction;

    private List<ChatMessage> messages;

    // ---NETWORKING---
    List<byte> receivedData = new List<byte>();
    int expectedLength = 0;
    const int CHUNK_SIZE = 8192;

    [Header("UI Elements")]
    public GameObject LLMUI;
    public GameObject RecordingUI;
    public GameObject ConfirmationUI;
    public TextMeshProUGUI ConfirmationText;
    public Button ConfirmButton;
    public Button CancelButton;

    [Header("Check for first interaction")]
    public GameManager gameManager;
    public bool firstInteraction = true;
    public bool apoptosisInteraction = false;

    void OnEnable(){
        triggerAction.action.started += StartRecording;
        triggerAction.action.canceled += StopRecording;
        triggerAction.action.Enable();
    }

    void OnDisable(){
        triggerAction.action.started -= StartRecording;
        triggerAction.action.canceled -= StopRecording;
        triggerAction.action.Disable();
    }

    // Start is called before the first frame update
    void Start()
    {
        //initialize the API
        api = new OpenAIAPI("");

        //set the inital prompt for the system
        messages = new List<ChatMessage>
        {
            SetPrompt()
        };

        recording = false;

        if (Microphone.devices.Length > 0)
        {
            StartCoroutine(WarmUpMicrophone());
        }
        else
        {
            Debug.LogError("No microphone detected.");
        }

        ConfirmButton.onClick.AddListener(() => ConfirmInput());
        CancelButton.onClick.AddListener(() => CancelInput());
    }

    // Custom prompt --(for later)--
    public void AddPrompt(string Prompt){
        messages.Add(new ChatMessage(ChatMessageRole.System, Prompt));
    }

    // Initial prompt
    ChatMessage SetPrompt(){
        //set the INITIAL prompt for the system
        return new ChatMessage(ChatMessageRole.System, "You are Alysia, a bot in a multi-user VR learning experience exploring DNA damage and repair." + 
        "The experience guides users through the processes of Homologous Recombination (HR) and Non-Homologous End-Joining (NHEJ). " +
        "Your task is to answer questions about the concept as well as the mechanical aspects of the experience (which will be given to you)." + 
        "ONLY answer from information given to you (if provided), and keep your responses simply worded (educational) and under 75 tokens. " +
        "You should have a warm, mentoring tone. Do not answer any questions not about the experience (i.e. not questions about DNA damage and repair or mechanics help, but DO answer questions similar to these topics), " +
        "simply reply with something like 'stay focused on the mission'.");
    }

    void ShowRecordingUI(){
        LLMUI.SetActive(true);
        RecordingUI.SetActive(true);
        ConfirmationUI.SetActive(false);

        //LLMUI.transform.parent = Camera.transform.parent;
    }

    void CloseRecordingUI(){
        LLMUI.SetActive(false);
    }

    public void SetApoptosisInteraction(){
        apoptosisInteraction = !apoptosisInteraction;
    }

    public void SetFirstInteraction(){
        firstInteraction = false;
    }

    void ShowConfirmationUI(string message){
        RecordingUI.SetActive(false);
        ConfirmationUI.SetActive(true);

        ConfirmationText.text = message;
    }

    /*
        Warmup the mic to stop freezing on first recording
    */
    IEnumerator WarmUpMicrophone()
    {
        if (Microphone.devices.Length == 0)
            yield break;

        string micName = Microphone.devices[0];

        AudioClip warmupClip = Microphone.Start(micName, false, 1, 16000);

        while (Microphone.GetPosition(micName) <= 0)
            yield return null;

        Microphone.End(micName);

        Debug.Log("Microphone warmed up.");
    }

    /*
        Start/Stop a recording
    */
    public void StartRecording(InputAction.CallbackContext ctx)
    {
        if (recording) return;

        string micName = Microphone.devices[0];

        recording = true;
        MicClip = Microphone.Start(micName, false, 30, 16000);

        Debug.Log("Recording started...");

        ShowRecordingUI();
    }

    public void StopRecording(InputAction.CallbackContext ctx)
    {
        if (!recording) return;

        string micName = Microphone.devices[0];

        int position = Microphone.GetPosition(micName);
        Microphone.End(micName);
        recording = false;

        if (position <= 0)
        {
            Debug.LogWarning("No audio recorded.");
            return;
        }

        float[] samples = new float[position];
        MicClip.GetData(samples, 0);

        AudioClip trimmedClip = AudioClip.Create(
            "TrimmedClip",
            position,
            1,
            16000,
            false
        );

        trimmedClip.SetData(samples, 0);

        Debug.Log("Recording stopped. Length: " + (position / 16000f) + " seconds");

        TranscribeAudio(trimmedClip);
    }

    /*
        Once a 10 second clip is recorded, convert the speech from the file to text through the API
    */
    async void TranscribeAudio(AudioClip clip)
    {
        //define filepath for the audio
        string filepath;

        //convert audio to .wav
        byte[] wavData = WavUtility.FromAudioClip(clip, out filepath, true); // Uses WavUtility (find in folder WavUtility)

        //transcribe the audio
        SpeechToText = await api.Transcriptions.GetTextAsync(filepath);

        //print it
        Debug.Log(SpeechToText);

        ShowConfirmationUI(SpeechToText);
    }

    void ConfirmInput(){
        if (IsServer)
        {
            AiTalk(SpeechToText);
        }
        else
        {
            RequestAiTalkServerRpc(SpeechToText);
        }

        CloseRecordingUI();
    }

    void CancelInput(){
        CloseRecordingUI();
    }

    /*
        NETWORK AITalk
    */
    [ServerRpc(RequireOwnership = false)]
    void RequestAiTalkServerRpc(string text)
    {
        AiTalk(text);
    }

    /*
        Call the OpenAI API --> input the user's words into the LLM, then convert the response into an audio clip to play back (partially from ChatGPT)
    */
    async void AiTalk(string input){
        //send the player input to the "user" end of the AI
        ChatMessage userMessage = new ChatMessage(ChatMessageRole.User, input);

        messages.Add(userMessage);

        //Generate response from the API
        var chatResult = await api.Chat.CreateChatCompletionAsync(new ChatRequest()
        {
            
            Model = "gpt-4o-mini",
            Temperature = 0.3,      //amount of fluff in the message
            MaxTokens = 75,         //max number of tokens in AI response
            Messages = messages
        });

        //Add that response to the chat on the Assistant end
        ChatMessage responseMessage = new ChatMessage(ChatMessageRole.Assistant, chatResult.Choices[0].Message.TextContent);

        messages.Add(responseMessage);

        // Call your API wrapper (ask for WAV format)
        using (Stream stream = await api.TextToSpeech.GetSpeechAsStreamAsync(
            input: chatResult.Choices[0].Message.TextContent,
            voice: "alloy",
            speed: 1.0,
            responseFormat: "wav",
            model: "tts-1"))
        {
            byte[] audioData;
            using (MemoryStream ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                audioData = ms.ToArray();
            }

            if (audioData == null || audioData.Length == 0)
            {
                Debug.LogError("TTS returned empty audio!");
                return;
            }

            if (IsServer)
            {
                SendAudio(audioData);
            }

            // Save for debugging
            string filePath = Path.Combine(Application.persistentDataPath, "tts_debug.wav");
            File.WriteAllBytes(filePath, audioData);
            Debug.Log("Saved WAV for inspection: " + filePath);

            //StartCoroutine(LoadWavFile(filePath));

            AudioClip clip = CreateAudioClipFromBytes(audioData, sampleRate: 24000, channels: 1);

            audioSource.clip = clip;
            StartCoroutine(PlayAndWait(audioSource));

            //audioSource.Play();


        }
    }

    void SendAudio(byte[] audioData)
    {
        for (int i = 0; i < audioData.Length; i += CHUNK_SIZE)
        {
            int size = Mathf.Min(CHUNK_SIZE, audioData.Length - i);
            byte[] chunk = new byte[size];
            Array.Copy(audioData, i, chunk, 0, size);

            ReceiveAudioChunkClientRpc(chunk, audioData.Length);
        }
    }

    [ClientRpc(RequireOwnership = false)]
    void ReceiveAudioChunkClientRpc(byte[] chunk, int totalLength)
    {
        // Skip sender (they already play it locally)
        if (IsOwner) return;

        if (expectedLength == 0)
        {
            expectedLength = totalLength;
            receivedData.Clear();
        }

        receivedData.AddRange(chunk);

        if (receivedData.Count >= expectedLength)
        {
            byte[] fullData = receivedData.ToArray();

            receivedData.Clear();
            expectedLength = 0;

            AudioClip clip = CreateAudioClipFromBytes(fullData, 24000, 1);
            audioSource.clip = clip;
            StartCoroutine(PlayAndWait(audioSource));
        }
    }

    // FUNCTION TO WAIT FOR ANSWER TO FINISH FOR FIRST INTERACTION
    IEnumerator PlayAndWait(AudioSource source)
    {
        source.Play();

        yield return new WaitWhile(() => source.isPlaying);

        Debug.Log("Audio finished");
        
        if(firstInteraction){
            gameManager.playPhase(1);
        }else if(apoptosisInteraction){
            gameManager.playPhase(4);
        }
    }

    /*
        Helper function to convert a wav file into an array of bytes (from ChatGPT)
    */
    float[] Convert16BitWavToFloats(byte[] wavBytes)
    {
        int headerOffset = 44; // Standard PCM header
        int sampleCount = (wavBytes.Length - headerOffset) / 2;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(wavBytes, headerOffset + i * 2);
            samples[i] = sample / 32768f; // convert to -1..1
        }

        return samples;
    }

    /*
        Helper function to convert an array of bytes into an AudioClip (from ChatGPT)
    */
    AudioClip CreateAudioClipFromBytes(byte[] wavBytes, int sampleRate = 24000, int channels = 1)
    {
        float[] samples = Convert16BitWavToFloats(wavBytes);
        AudioClip clip = AudioClip.Create("RuntimeClip", samples.Length, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

}