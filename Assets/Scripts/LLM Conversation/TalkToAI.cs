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

public class TalkToAI : MonoBehaviour
{
    
    [SerializeField] private AudioClip MicClip;
    [SerializeField] private string SpeechToText;

    float timer = 10f;
    bool timerFinished = false;
    private bool recording;

    public AudioSource audioSource;
    private AudioClip audioClip;

    private OpenAIAPI api;

    public InputActionProperty triggerAction;

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

        //set the prompt for the system
        new ChatMessage(ChatMessageRole.System, "You are a my friend, keep responses EXTREMELY brief, no longer than 3 sentences. YOUR FAVORITE COLOR IS BLUE");

        recording = false;

        if (Microphone.devices.Length > 0)
        {
            StartCoroutine(WarmUpMicrophone());
        }
        else
        {
            Debug.LogError("No microphone detected.");
        }
    }

    // Update is called once per frame
    void Update()
    {

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
        AiTalk();
    }

    /*
        Call the OpenAI API --> input the user's words into the LLM, then convert the response into an audio clip to play back (partially from ChatGPT)
    */
    async void AiTalk(){
        //send the player input to the "user" end of the AI
        ChatMessage userMessage = new ChatMessage(ChatMessageRole.User, SpeechToText);

        //Generate response from the API
        var chatResult = await api.Chat.CreateChatCompletionAsync(new ChatRequest()
        {
            
            Model = "gpt-4o-mini",
            Temperature = 0.3,      //amount of fluff in the message
            MaxTokens = 75,         //max number of tokens in AI response
            Messages = new ChatMessage[] {
                userMessage
            }
        });

        //Add that response to the chat on the Assistant end
        ChatMessage responseMessage = new ChatMessage(ChatMessageRole.Assistant, chatResult.Choices[0].Message.TextContent);


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

            // Save for debugging
            string filePath = Path.Combine(Application.persistentDataPath, "tts_debug.wav");
            File.WriteAllBytes(filePath, audioData);
            Debug.Log("Saved WAV for inspection: " + filePath);

            //StartCoroutine(LoadWavFile(filePath));

            AudioClip clip = CreateAudioClipFromBytes(audioData, sampleRate: 24000, channels: 1);

            audioSource.clip = clip;
            audioSource.Play();
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