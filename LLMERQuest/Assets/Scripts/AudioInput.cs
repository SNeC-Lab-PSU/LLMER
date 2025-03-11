using OpenAI_API;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class AudioInput : MonoBehaviour
{
    public Sprite playIcon;
    public Sprite stopIcon;
    public Button playButton;
    private Image buttonImage;
    public TextMeshProUGUI botText;
    public TextMeshProUGUI userText;
    public int maxFontSize = 40;
    public int minFontSize = 5;

    public GameObject testSpeaker;

    [SerializeField] private OVRHand funcHand;

    AudioClip recording;
    AudioSource audioSource;
    private float startRecordingTime;
    bool isTTSProcessing = false;
    bool isSTTProcessing = false;
    string devicename = null;
    private int recordingStartSample = 0;
    bool isRecording = false;
    int recordBufLen = 10; // seconds

    private ConcurrentQueue<string> MsgPool = new ConcurrentQueue<string>();

    OpenAIAPI api;
    OpenAI_API.Chat.Conversation chat;

    List<float> TimeEndRecord = new List<float>();
    List<float> TimeSendWhisper = new List<float>();
    List<float> TimeRecvWhisper = new List<float>();
    List<float> TimeSendTTS = new List<float>();
    List<float> TimeRecvTTS = new List<float>();
    List<float> TimeReact = new List<float>();
    List<int> CountTTS = new List<int>();

    ChatControl chatControl = null;
    // make sure new text will not overwrite the old one until the audio is finished
    public AudioState state = AudioState.Finished;
    public enum AudioState
    {
        Prepare,
        Playing,
        Finished
    }

    void Awake()
    {
        // check platform
        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            // initialize the API, PC version from the file
            api = new OpenAIAPI(APIAuthentication.LoadFromPath("../", ".env"));
        }
        else
        {
            // mobile version from the API key
            api = new OpenAIAPI(Utils.OPENAI_API_KEY);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        buttonImage = playButton.GetComponent<Image>();
        buttonImage.sprite = playIcon;

        InitializeRecording();

        chatControl = this.GetComponent<ChatControl>();
    }

    private void Update()
    {
        bool isCurrentlyTriggered = (funcHand != null && funcHand.GetFingerIsPinching(OVRHand.HandFinger.Middle)) ||
            OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch) ||
            Input.GetKey(KeyCode.R);
        if (!isRecording && isCurrentlyTriggered)
        {
            StartRecording();
        }

        if (isRecording && (!isCurrentlyTriggered))
        {
            StopRecording();
        }

        if (state == AudioState.Playing)
        {
            if (!audioSource.isPlaying)
            {
                state = AudioState.Finished;
                Debug.Log("Finished Audio Playing.");
            }
        }
        if (state == AudioState.Finished && !MsgPool.IsEmpty)
        {
            if (MsgPool.TryDequeue(out string msg))
            {
                GetAudioSpeech(msg);
            }
        }
    }

    public void EnqueAudioMsg(string msg)
    {
        MsgPool.Enqueue(msg);
    }

    public async void GetAudioSpeech(string text)
    {
        isTTSProcessing = true;
        if (chatControl.EvaMode)
        {
            CountTTS.Add(text.Length);
            TimeSendTTS.Add(Time.realtimeSinceStartup);
        }
        state = AudioState.Prepare;
        botText.text = "Bot: " + text;
        string filename = "botRes.mp3"; // Replace with your actual file path
        filename = Path.Combine(Application.persistentDataPath, filename);
        await api.TextToSpeech.SaveSpeechToFileAsync(text, filename);
        StartCoroutine(GetAudioClipCoroutine(filename));
    }

    IEnumerator GetAudioClipCoroutine(string filePath)
    {
        string url = "file://" + filePath;
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(www.error);
            }
            else
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                    state = AudioState.Playing;
                }
                else
                {
                    state = AudioState.Finished;
                }
                if (chatControl.EvaMode)
                {
                    TimeRecvTTS.Add(Time.realtimeSinceStartup);
                    if (TimeReact.Count < TimeEndRecord.Count)
                    {
                        TimeReact.Add(Time.realtimeSinceStartup - TimeEndRecord[TimeReact.Count]);
                    }
                    else if (chatControl.EvaMode && TimeReact.Count < TimeSendWhisper.Count)
                    {
                        TimeReact.Add(Time.realtimeSinceStartup - TimeSendWhisper[TimeReact.Count]);
                    }
                }
                isTTSProcessing = false;
            }
        }
    }

    public async void GetAudioTranscription(string path)
    {
        isSTTProcessing = true;
        if (chatControl.EvaMode)
            TimeSendWhisper.Add(Time.realtimeSinceStartup);
        string resultText = await api.Transcriptions.GetTextAsync(path, "en");
        Debug.Log(resultText);
        if (chatControl.EvaMode)
            TimeRecvWhisper.Add(Time.realtimeSinceStartup);
        // only send the result to chatbot when the result is not empty and the length is resonably long more than two words
        if (!string.IsNullOrEmpty(resultText))
        {
            string[] strings = resultText.Split(' ');
            if (strings.Length > 1 || strings[0].ToLower().Contains("hi") || strings[0].ToLower().Contains("hello") || strings[0].ToLower().Contains("hey"))
            {
                userText.text = "User: " + resultText;
                await chatControl.GetResponse(resultText);
            }
        }
        isSTTProcessing = false;
    }

    void InitializeRecording()
    {
        audioSource = testSpeaker.GetComponent<AudioSource>();
        // get the device name of microphone
        foreach (var device in Microphone.devices)
        {
            Debug.Log("Audio Source Name: " + device);
            devicename = device;
            if (device.ToString().Contains("Oculus"))
            {
                Debug.Log("Oculus Audio Source detected.");
                break;
            }
        }
        //Get the max frequency of a microphone, if it's less than 44100 record at the max frequency, else record at 44100
        int minFreq;
        int maxFreq;
        int freq = 44100;
        Microphone.GetDeviceCaps(devicename, out minFreq, out maxFreq);
        if (maxFreq < 44100)
            freq = maxFreq;
        //Start the recording
        recording = Microphone.Start(devicename, true, recordBufLen, freq);
        Debug.Log("Recording started.");
    }

    void StartRecording()
    {
        if (recording == null)
        {
            Debug.Log("Recording is null, not start");
            return;
        }
        recordingStartSample = Microphone.GetPosition(devicename);
        startRecordingTime = Time.time;
        buttonImage.sprite = stopIcon;
        isRecording = true;
    }

    void StopRecording()
    {
        if (recording == null)
        {
            Debug.Log("Recording is null, not end");
            return;
        }
        //End the recording 
        int recordingEndSample = Microphone.GetPosition(devicename);
        if (chatControl.EvaMode)
            TimeEndRecord.Add(Time.realtimeSinceStartup);
        var RecordSamples = (recordingEndSample - recordingStartSample + recordBufLen * recording.frequency) % (recordBufLen * recording.frequency);
        var RecordTime = (float)(RecordSamples) / recording.frequency;

        // pass to next step once the record time is valid
        if (RecordTime > 0.5f)
        {
            //Trim the audioclip by the length of the recording
            AudioClip truncatedClip = TruncateClip(recording, recordingStartSample, recordingEndSample);

            // Save the recording as a WAV file
            string filepath = SaveWav("recorded_audio", truncatedClip);

            GetAudioTranscription(filepath);
        }
        buttonImage.sprite = playIcon;
        isRecording = false;
    }

    string SaveWav(string filename, AudioClip clip)
    {
        if (!filename.ToLower().EndsWith(".wav"))
        {
            filename += ".wav";
        }

        var filepath = Path.Combine(Application.persistentDataPath, filename);

        int HEADER_SIZE = 44;

        // Create header
        var fileStream = new FileStream(filepath, FileMode.Create);
        byte[] header = new byte[HEADER_SIZE];
        fileStream.Write(header, 0, header.Length);

        // Convert and write audio data
        var audioData = new float[clip.samples * clip.channels];
        clip.GetData(audioData, 0);
        var bytesData = ConvertAndWrite(fileStream, audioData);

        // Write header again with updated data
        WriteHeader(fileStream, clip, header, bytesData);

        fileStream.Close();

        return filepath; // TODO: return false if there's a failure
    }

    static int ConvertAndWrite(FileStream fileStream, float[] audioData)
    {
        var bytesData = new byte[audioData.Length * 2];
        var rescaleFactor = 32767;
        for (int i = 0; i < audioData.Length; i++)
        {
            short value = (short)(audioData[i] * rescaleFactor);
            BitConverter.GetBytes(value).CopyTo(bytesData, i * 2);
        }

        fileStream.Write(bytesData, 0, bytesData.Length);
        return bytesData.Length;
    }

    static void WriteHeader(FileStream fileStream, AudioClip clip, byte[] header, int bytesDataLength)
    {
        var hz = clip.frequency;
        var channels = clip.channels;
        var samples = clip.samples;

        fileStream.Seek(0, SeekOrigin.Begin);

        byte[] riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
        riff.CopyTo(header, 0);
        BitConverter.GetBytes(header.Length + bytesDataLength - 8).CopyTo(header, 4);

        byte[] wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
        wave.CopyTo(header, 8);

        byte[] fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
        fmt.CopyTo(header, 12);
        BitConverter.GetBytes(16).CopyTo(header, 16); // sub chunk size
        BitConverter.GetBytes((ushort)1).CopyTo(header, 20); // PCM
        BitConverter.GetBytes((ushort)channels).CopyTo(header, 22);
        BitConverter.GetBytes(hz).CopyTo(header, 24);
        BitConverter.GetBytes(hz * channels * 2).CopyTo(header, 28); // byte rate
        BitConverter.GetBytes((ushort)(channels * 2)).CopyTo(header, 32); // block align
        BitConverter.GetBytes((ushort)16).CopyTo(header, 34); // bits per sample

        byte[] data = System.Text.Encoding.UTF8.GetBytes("data");
        data.CopyTo(header, 36);
        BitConverter.GetBytes(samples * channels * 2).CopyTo(header, 40); // sub chunk 2 size

        fileStream.Write(header, 0, header.Length);
    }

    private AudioClip TruncateClip(AudioClip originalClip, int startSample, int endSample)
    {
        int sampleCount = originalClip.frequency * recordBufLen; // Total samples in the buffer
        int lengthSamples = (endSample - startSample + sampleCount) % sampleCount;
        AudioClip newClip = AudioClip.Create("TruncatedClip", lengthSamples, originalClip.channels, originalClip.frequency, false);

        float[] data = new float[lengthSamples * originalClip.channels];
        if (endSample > startSample)
        {
            originalClip.GetData(data, startSample);
        }
        else
        {
            int firstPartSampleCount = originalClip.samples - startSample; // From startSample to end of buffer
            int secondPartSampleCount = endSample; // From start of buffer to endSample

            float[] firstPart = new float[firstPartSampleCount * originalClip.channels];
            float[] secondPart = new float[secondPartSampleCount * originalClip.channels];
            recording.GetData(firstPart, startSample);
            recording.GetData(secondPart, 0);

            firstPart.CopyTo(data, 0);
            secondPart.CopyTo(data, firstPartSampleCount * originalClip.channels);

        }
        newClip.SetData(data, 0);

        return newClip;
    }

    public List<float> GetTimeEndRecord()
    {
        return TimeEndRecord;
    }

    public List<float> GetTimeSendWhisper()
    {
        return TimeSendWhisper;
    }

    public List<float> GetTimeRecvWhisper()
    {
        return TimeRecvWhisper;
    }

    public List<float> GetTimeSendTTS()
    {
        return TimeSendTTS;
    }

    public List<float> GetTimeRecvTTS()
    {
        return TimeRecvTTS;
    }

    public List<int> GetCountTTS()
    {
        return CountTTS;
    }

    public List<float> GetTimeReact()
    {
        return TimeReact;
    }

    public bool IsQueueEmpty()
    {
        return MsgPool.IsEmpty && (!audioSource.isPlaying);
    }
    public bool IsTTSProcessing()
    {
        return isTTSProcessing;
    }
    public bool IsSTTProcessing()
    {
        return isSTTProcessing;
    }

    private void OnDestroy()
    {
        if (devicename != null && Microphone.IsRecording(devicename))
        {
            Microphone.End(devicename);
            Debug.Log("Microphone recording ended.");
        }
    }
}
