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

public class DEV_VoiceLineGenerator : MonoBehaviour
{

    public AudioSource audioSource;

    public String VoiceLine;

    private OpenAIAPI api;

    // Start is called before the first frame update
    void Start()
    {
        api = new OpenAIAPI("");

        GenerateVoiceLine();
    }

    async void GenerateVoiceLine(){
        using (Stream stream = await api.TextToSpeech.GetSpeechAsStreamAsync(
            input: VoiceLine,
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
