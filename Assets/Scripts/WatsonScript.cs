using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IBM.Watson.Assistant.V2;
using IBM.Watson.Assistant.V2.Model;
using IBM.Watson.SpeechToText.V1;
using IBM.Watson.TextToSpeech.V1;
using IBM.Cloud.SDK;
using IBM.Cloud.SDK.Utilities;
using IBM.Cloud.SDK.DataTypes;
using IBM.Cloud.SDK.Authentication.Iam;
using UnityEngine.UI;

public class WatsonScript : MonoBehaviour
{
    #region PLEASE SET THESE VARIABLES IN THE INSPECTOR
    [Header("Watson Assistant")]
    [Tooltip("The service URL (optional). This defaults to \"https://gateway.watsonplatform.net/assistant/api\"")]
    [SerializeField]
    private string AssistantURL;
    [SerializeField]
    private string assistantId;
    [Tooltip("The apikey.")]
    [SerializeField]
    private string assistantIamApikey;
    [Tooltip("The version date with which you would like to use the service in the form YYYY-MM-DD.")]
    [SerializeField]
    private string versionDate;

    [Header("Speech to Text")]
    [Tooltip("The service URL (optional). This defaults to \"https://stream.watsonplatform.net/speech-to-text/api\"")]
    [SerializeField]
    private string SpeechToTextURL;
    [Tooltip("The apikey.")]
    [SerializeField]
    private string SpeechToTextIamApikey;

    [Header("Text to Speech")]
    [SerializeField]
    [Tooltip("The service URL (optional). This defaults to \"https://stream.watsonplatform.net/text-to-speech/api\"")]
    private string TextToSpeechURL;
    [Tooltip("The apikey.")]
    [SerializeField]
    private string TextToSpeechIamApikey;

    #endregion

    private int _recordingRoutine = 0;
    private string _microphoneID = null;
    private AudioClip _recording = null;
    private int _recordingBufferSize = 2;
    private int _recordingHZ = 22050;
    private AssistantService _assistant;
    private SpeechToTextService _speechToText;
    private TextToSpeechService _textToSpeech;
    private string sessionId;
    private bool firstMessage;
    private bool _stopListeningFlag = false;
    private bool sessionCreated = false;

    public AudioSource audioObject;
    public GameObject player;
    Animator animator;

    //private fsSerializer _serializer = new fsSerializer();

    public Dictionary<string, object> inputObj = new Dictionary<string, object>();

    //Animator animator;

    //Get your services up and running
    //void Awake()
    //{
    //    Runnable.Run(InitializeServices());
    //}

    // Use this for initialization
    private void Start()
    {
        LogSystem.InstallDefaultReactors();
        Runnable.Run(InitializeServices());
        animator = player.GetComponent<Animator>();
    }

    private IEnumerator InitializeServices()
    {
        //Assistant
        if (string.IsNullOrEmpty(assistantIamApikey))
        {
            throw new IBMException("Plesae provide IAM ApiKey for the Assistant.");
        }
        
        IamAuthenticator assAuthenticator = new IamAuthenticator(apikey: assistantIamApikey);

        while (!assAuthenticator.CanAuthenticate())
            yield return null;

        _assistant = new AssistantService(versionDate, assAuthenticator);
        if (!string.IsNullOrEmpty(AssistantURL))
        {
            _assistant.SetServiceUrl(AssistantURL);
        }

        _assistant.CreateSession(OnCreateSession, assistantId);

        while (!sessionCreated)
            yield return null;

        //Text To Speech
        if (string.IsNullOrEmpty(TextToSpeechIamApikey))
        {
            throw new IBMException("Please provide IAM ApiKey for the TTS.");
        }
        
        IamAuthenticator TTSAuthenticator = new IamAuthenticator(apikey: TextToSpeechIamApikey);

        while (!TTSAuthenticator.CanAuthenticate())
            yield return null;

        _textToSpeech = new TextToSpeechService(TTSAuthenticator);
        if (!string.IsNullOrEmpty(TextToSpeechURL))
        {
            _textToSpeech.SetServiceUrl(TextToSpeechURL);
        }

        //Speech To Text
        if (string.IsNullOrEmpty(SpeechToTextIamApikey))
        {
            throw new IBMException("Please provide IAM ApiKey for the STT.");
        }

        IamAuthenticator STTAuthenticator = new IamAuthenticator(apikey: SpeechToTextIamApikey);


        while (!STTAuthenticator.CanAuthenticate())
            yield return null;

        _speechToText = new SpeechToTextService(STTAuthenticator);
        if (!string.IsNullOrEmpty(SpeechToTextURL))
        {
            _speechToText.SetServiceUrl(SpeechToTextURL);
        }
        _speechToText.StreamMultipart = true;

        Active = true;

        // Send first message, create inputObj w/ no context
        Message0();

        StartRecording();   // Setup recording

    }

    //  Initiate a conversation
    private void Message0()
    {
        firstMessage = true;
        var input = new MessageInput()
        {
            Text = "Hello"
        };

        _assistant.Message(OnMessage, assistantId, sessionId, input);
    }


    private void OnMessage(DetailedResponse<MessageResponse> response, IBMError error)
    {
        if (!firstMessage)
        {
            //getIntent
            if (response.Result.Output.Intents.Capacity > 0)
            {
                string intent = response.Result.Output.Intents[0].Intent;
                Debug.Log(intent);
                if (intent.Equals("General_Greetings"))
                {
                   animator.SetTrigger("WaveTrigger");
                } if (intent.Equals("General_Ending"))
                {
                    animator.SetTrigger("WaveTrigger");
                }
            }
            //get Watson Output
            string outputText2 = response.Result.Output.Generic[0].Text;


            CallTextToSpeech(outputText2);
        }

        firstMessage = false;

    }

    private void BuildSpokenRequest(string spokenText)
    {
        var input = new MessageInput()
        {
            Text = spokenText
        };

        _assistant.Message(OnMessage, assistantId, sessionId, input);
    }

    private void CallTextToSpeech(string outputText)
    {
        Debug.Log("Sent to Watson Text To Speech: " + outputText);

        byte[] synthesizeResponse = null;
        AudioClip clip = null;

        _textToSpeech.Synthesize(
            callback: (DetailedResponse<byte[]> response, IBMError error) =>
            {
                synthesizeResponse = response.Result;
                clip = WaveFile.ParseWAV("myClip", synthesizeResponse);
                PlayClip(clip);

            },
            text: outputText,
            voice: "en-US_MichaelVoice",
            accept: "audio/wav"
        );
    }

    private void PlayClip(AudioClip clip)
    {
        Debug.Log("Received audio file from Watson Text To Speech");

        if (Application.isPlaying && clip != null)
        {
            //GameObject audioObject = new GameObject("AudioObject");
            //AudioSource source = audioObject.AddComponent<AudioSource>();
            audioObject.spatialBlend = 0.0f;
            audioObject.volume = 1.0f;
            audioObject.loop = false;
            audioObject.clip = clip;
            audioObject.Play();

            Invoke("RecordAgain", audioObject.clip.length);
            //Destroy(audioObject, clip.length);
        }
    }

    private void RecordAgain()
    {
        Debug.Log("Played Audio received from Watson Text To Speech");
        if (!_stopListeningFlag)
        {
            OnListen();
        }
    }

    private void OnListen()
    {
        Log.Debug("AvatarPattern.OnListen", "Start();");

        Active = true;

        StartRecording();
    }

    public bool Active
    {
        get { return _speechToText.IsListening; }
        set
        {
            if (value && !_speechToText.IsListening)
            {
                _speechToText.DetectSilence = true;
                _speechToText.EnableWordConfidence = false;
                _speechToText.EnableTimestamps = false;
                _speechToText.SilenceThreshold = 0.03f;
                _speechToText.MaxAlternatives = 1;
                _speechToText.EnableInterimResults = true;
                _speechToText.OnError = OnError;
                _speechToText.StartListening(OnRecognize);
            }
            else if (!value && _speechToText.IsListening)
            {
                _speechToText.StopListening();
            }
        }
    }

    private void OnRecognize(SpeechRecognitionEvent result)
    {
        if (result != null && result.results.Length > 0)
        {
            foreach (var res in result.results)
            {
                foreach (var alt in res.alternatives)
                {
                    if (res.final && alt.confidence > 0)
                    {
                        StopRecording();
                        string text = alt.transcript;
                        Debug.Log("Watson hears : " + text + " Confidence: " + alt.confidence);
                        BuildSpokenRequest(text);
                    }
                }
            }
        }

    }


    private void StartRecording()
    {
        if (_recordingRoutine == 0)
        {
            Debug.Log("Started Recording");
            UnityObjectUtil.StartDestroyQueue();
            _recordingRoutine = Runnable.Run(RecordingHandler());
        }
    }

    private void StopRecording()
    {
        if (_recordingRoutine != 0)
        {
            Debug.Log("Stopped Recording");
            Microphone.End(_microphoneID);
            Runnable.Stop(_recordingRoutine);
            _recordingRoutine = 0;
        }
    }

    private void OnError(string error)
    {
        Active = false;

        Log.Debug("AvatarPatternError.OnError", "Error! {0}", error);
    }

    private void OnCreateSession(DetailedResponse<SessionResponse> response, IBMError error)
    {
        Log.Debug("AvatarPatternError.OnCreateSession()", "Session: {0}", response.Result.SessionId);
        sessionId = response.Result.SessionId;
        sessionCreated = true;
    }

    private IEnumerator RecordingHandler()
    {
        _recording = Microphone.Start(_microphoneID, true, _recordingBufferSize, _recordingHZ);
        yield return null;      // let m_RecordingRoutine get set..

        if (_recording == null)
        {
            StopRecording();
            yield break;
        }

        bool bFirstBlock = true;
        int midPoint = _recording.samples / 2;
        float[] samples = null;

        while (_recordingRoutine != 0 && _recording != null)
        {
            int writePos = Microphone.GetPosition(_microphoneID);
            if (writePos > _recording.samples || !Microphone.IsRecording(_microphoneID))
            {
                Log.Error("MicrophoneWidget", "Microphone disconnected.");

                StopRecording();
                yield break;
            }

            if ((bFirstBlock && writePos >= midPoint)
                || (!bFirstBlock && writePos < midPoint))
            {
                // front block is recorded, make a RecordClip and pass it onto our callback.
                samples = new float[midPoint];
                _recording.GetData(samples, bFirstBlock ? 0 : midPoint);

                AudioData record = new AudioData();
                record.MaxLevel = Mathf.Max(samples);
                record.Clip = AudioClip.Create("Recording", midPoint, _recording.channels, _recordingHZ, false);
                record.Clip.SetData(samples, 0);

                _speechToText.OnListen(record);

                bFirstBlock = !bFirstBlock;
            }
            else
            {
                // calculate the number of samples remaining until we ready for a block of audio,
                // and wait that amount of time it will take to record.
                int remaining = bFirstBlock ? (midPoint - writePos) : (_recording.samples - writePos);
                float timeRemaining = (float)remaining / (float)_recordingHZ;

                yield return new WaitForSeconds(timeRemaining);
            }

        }

        yield break;
    }
}
