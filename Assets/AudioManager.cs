using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;
    public AudioSource Source { get; private set; }
    public bool clipReady = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            Source = GetComponent<AudioSource>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadSong(string songFolder)
    {
        clipReady = false;
        Source.Stop();
        Source.clip = null;
        StartCoroutine(LoadRoutine(songFolder));
    }

    IEnumerator LoadRoutine(string songFolder)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "map", songFolder, "song.wav");
        string url = "file://" + filePath;

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Source.clip = DownloadHandlerAudioClip.GetContent(www);
                clipReady = true;
                Debug.Log($"[AudioManager] clip loaded, length={Source.clip.length}s");
            }
            else
            {
                Debug.LogError("음악 로드 실패: " + www.error);
            }
        }
    }

    public void Play() => Source.Play();
    public void Pause() => Source.Pause();
    public float TimeMs => Source.time * 1000f;
    public bool IsPlaying => Source.isPlaying;
}