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
        string filePath = Path.Combine(SongMedia.FolderPath(songFolder), "song.wav");
        // 한글/공백/# 같은 문자가 들어간 폴더 이름도 되도록 URL로 제대로 변환
        string url = new System.Uri(filePath).AbsoluteUri;

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

    // AudioSource.time은 오디오 버퍼 단위로 뚝뚝 끊겨서 올라가므로,
    // 실제 시계로 부드럽게 흘려보내고 오디오가 새 위치를 알려줄 때마다 살짝씩 맞춰준다
    const double snapThreshold = 0.1;   // 이보다 많이 어긋나면 바로 맞춤 (시작 직후, 렉 등)
    const double correctionRate = 0.1;  // 조금 어긋났을 때 한 번에 따라가는 비율

    private bool playing = false;
    private bool waitingToStart = false;
    private double smoothTime = 0;
    private double lastReported = -1;
    private double lastRealtime = 0;
    private int cachedFrame = -1;

    // 곡 시간을 -leadInSeconds부터 흘려보내고, 0이 되는 순간 노래를 튼다
    // (그동안 노트는 미리 내려오고 유저는 준비할 시간을 갖는다)
    public void Play(float leadInSeconds = 0f)
    {
        playing = true;
        smoothTime = -leadInSeconds;
        lastReported = -1;
        lastRealtime = Time.realtimeSinceStartupAsDouble;
        cachedFrame = -1;

        waitingToStart = leadInSeconds > 0f;
        if (!waitingToStart) Source.Play();
    }

    // 노래가 실제로 나오기 시작했는지 (준비 시간이 끝났는지)
    public bool MusicStarted => playing && !waitingToStart;

    void Update()
    {
        // 준비 시간 중에도 시간이 흘러 제때 노래가 시작되도록 매 프레임 갱신
        if (playing) _ = TimeMs;
    }

    public void Pause()
    {
        Source.Pause();
        playing = false;
    }

    // 한 프레임 안에서는 모든 스크립트가 같은 값을 보도록 프레임마다 한 번만 계산
    public float TimeMs
    {
        get
        {
            if (cachedFrame != Time.frameCount)
            {
                cachedFrame = Time.frameCount;
                UpdateSmoothTime();
            }
            // 싱크 보정: 소리가 늦게 들리는 환경이면 곡 시간을 그만큼 늦춰서 노트/판정/영상을 같이 민다
            return (float)(smoothTime * 1000.0) - SyncOffset.Ms;
        }
    }

    void UpdateSmoothTime()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        double delta = now - lastRealtime;
        lastRealtime = now;

        if (!playing) return;

        if (waitingToStart)
        {
            smoothTime += delta;
            if (smoothTime >= 0)
            {
                // 시간을 0으로 되돌리지 않고 그대로 흘려보낸다 (노트가 멈칫하지 않게).
                // 넘친 몇 ms는 아래 오디오 위치 보정이 알아서 맞춘다
                waitingToStart = false;
                Source.Play();
            }
            return;
        }

        // 곡이 끝나면 시간을 멈춘다 (끝난 뒤 0으로 되돌아가는 것 방지)
        if (!Source.isPlaying || Source.clip == null) return;

        double previous = smoothTime;
        smoothTime += delta;

        double reported = (double)Source.timeSamples / Source.clip.frequency;
        if (reported != lastReported)
        {
            lastReported = reported;
            double error = reported - smoothTime;

            if (System.Math.Abs(error) > snapThreshold)
            {
                smoothTime = reported;
                return;
            }

            smoothTime += error * correctionRate;
        }

        // 작은 보정 때문에 시간이 뒤로 가지 않게
        if (smoothTime < previous)
            smoothTime = previous;
    }

    public bool IsPlaying => Source.isPlaying;
}