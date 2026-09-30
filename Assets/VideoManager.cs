using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using System.IO;

// 게임 화면 배경 영상. 곡 폴더의 video.webm(없으면 video.mp4)을 RenderTexture로 받아 화면 뒤의 Quad에 그린다.
public class VideoManager : MonoBehaviour
{
    public static VideoManager instance;
    public bool videoReady = false;

    const string gameSceneName = "gamescreen";

    // 노래와 이만큼(초) 넘게 어긋나면 영상 위치를 노래에 맞춘다
    const double resyncThreshold = 0.12;
    const float resyncCheckInterval = 1f;

    private VideoPlayer player;
    private RenderTexture targetTexture;
    private Transform quadTransform;
    private MeshRenderer quadRenderer;
    private Material quadMaterial;
    private bool hasVideo = false;
    private bool playing = false;
    private float nextResyncCheck = 0f;
    private bool seeking = false;
    private double lastCheckedVideoTime = -1;
    private bool stallWarned = false;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        player = GetComponent<VideoPlayer>();
        if (player == null) player = gameObject.AddComponent<VideoPlayer>();

        player.playOnAwake = false;
        player.waitForFirstFrame = true;
        player.isLooping = true;
        player.skipOnDrop = true;
        player.source = VideoSource.Url;
        player.renderMode = VideoRenderMode.RenderTexture;
        // 오디오는 AudioManager가 따로 튼다. 영상은 아래 Update에서 노래 위치와 비교해 맞춘다
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.timeUpdateMode = VideoTimeUpdateMode.GameTime;

        player.errorReceived += OnVideoError;
        player.prepareCompleted += OnPrepared;
        player.seekCompleted += vp => seeking = false;
        // Quad를 쓰면 UV가 항상 텍스처 전체(0~1)를 그대로 쓴다
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "VideoQuad";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = new Vector3(0f, 0f, 5f);   // 노트/트랙(z=0)보다 뒤
        quad.transform.localRotation = Quaternion.identity;

        quadTransform = quad.transform;
        quadRenderer = quad.GetComponent<MeshRenderer>();
        quadRenderer.enabled = false;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit != null)
        {
            quadMaterial = new Material(unlit);
            // 주의: renderer.material을 "읽으면" 재질 복사본이 생겨서, 아래에서 quadMaterial에 넣은
            // 영상 텍스처가 화면에 안 나오고 흰 화면이 된다. 반드시 sharedMaterial만 쓴다.
            quadRenderer.sharedMaterial = quadMaterial;
        }
        else
        {
            Debug.LogError("[VideoManager] Universal Render Pipeline/Unlit 셰이더를 못 찾았어요. 배경 영상 없이 진행");
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ReleaseTexture();
    }

    // 게임 화면이 아니면 영상을 멈추고 숨긴다 (곡 선택/결과 화면 뒤에 마지막 장면이 남지 않게)
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != gameSceneName) StopVideo();
    }

    public void LoadVideo(string songFolder)
    {
        StopVideo();
        videoReady = false;

        string folder = SongMedia.FolderPath(songFolder);
        string filePath = SongMedia.FindVideo(folder, "video");
        if (filePath == null || quadMaterial == null)
        {
            Debug.Log($"[VideoManager] 영상 없이 진행: {folder}");
            videoReady = true;
            return;
        }

        player.url = filePath;
        player.Prepare();
    }

    void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError($"[VideoManager] 영상 오류, 배경 없이 진행: {message}");
        StopVideo();
        videoReady = true;
    }

    void OnPrepared(VideoPlayer vp)
    {
        int w = (int)vp.width;
        int h = (int)vp.height;
        if (w <= 0 || h <= 0)
        {
            Debug.LogError("[VideoManager] 영상 크기를 알 수 없어서 배경 없이 진행");
            videoReady = true;
            return;
        }

        // 영상 해상도에 맞춘 RenderTexture (깊이 버퍼는 필요 없음)
        ReleaseTexture();
        targetTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        targetTexture.name = "VideoTexture";
        targetTexture.Create();
        player.targetTexture = targetTexture;

        quadMaterial.SetTexture("_BaseMap", targetTexture);
        quadMaterial.mainTexture = targetTexture;

        FitToScreen(w, h);
        // 판은 재생을 시작할 때 켠다 (그 전에 빈 판이 보이지 않게)
        hasVideo = true;
        videoReady = true;
        Debug.Log($"[VideoManager] 준비 완료: {w}x{h}, {vp.frameRate:F0}fps, {vp.length:F1}s");
    }

    // 영상이 16:9 게임 화면을 꽉 채우도록 (넘치는 쪽은 잘림)
    void FitToScreen(int w, int h)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float screenH = cam.orthographicSize * 2f;
        float screenW = screenH * cam.aspect;
        float videoAspect = (float)w / h;

        Vector3 scale = videoAspect > screenW / screenH
            ? new Vector3(screenH * videoAspect, screenH, 1f)
            : new Vector3(screenW, screenW / videoAspect, 1f);

        Vector3 camPos = cam.transform.position;
        quadTransform.position = new Vector3(camPos.x, camPos.y, 5f);
        quadTransform.localScale = scale;
    }

    public void Play()
    {
        if (!hasVideo) return;

        // 노래가 이미 조금 진행됐으면 그 위치부터 (처음이면 굳이 이동하지 않음)
        double target = CurrentSongTime();
        if (target > resyncThreshold) SeekTo(target);

        player.Play();
        quadRenderer.enabled = true;
        playing = true;
        lastCheckedVideoTime = -1;
        stallWarned = false;
        nextResyncCheck = Time.unscaledTime + resyncCheckInterval;
    }

    void SeekTo(double time)
    {
        seeking = true;
        player.time = time;
    }

    public void Pause()
    {
        if (!hasVideo) return;
        player.Pause();
        playing = false;
    }

    void StopVideo()
    {
        playing = false;
        hasVideo = false;
        seeking = false;
        if (player != null) player.Stop();
        if (quadRenderer != null) quadRenderer.enabled = false;
    }

    void Update()
    {
        if (!playing || !player.isPlaying || seeking) return;
        if (Time.unscaledTime < nextResyncCheck) return;
        nextResyncCheck = Time.unscaledTime + resyncCheckInterval;

        // 영상이 1초 동안 전혀 안 움직였으면 디코더가 멈춘 것. 이동 명령을 반복하면 더 꼬이므로 경고만 남긴다
        double videoTime = player.time;
        bool stalled = lastCheckedVideoTime >= 0 && System.Math.Abs(videoTime - lastCheckedVideoTime) < 0.001;
        lastCheckedVideoTime = videoTime;
        if (stalled)
        {
            if (!stallWarned)
            {
                stallWarned = true;
                Debug.LogWarning($"[VideoManager] 영상이 {videoTime:F2}s에서 멈춰 있어요. mp4(H.264)는 PC에 따라 멈출 수 있으니 VP8 video.webm으로 바꿔 주세요: {player.url}");
            }
            return;
        }

        // 영상이 노래보다 밀리거나 앞서면 노래 위치로 맞춘다 (자주 하면 끊기므로 1초에 한 번만 검사)
        double target = CurrentSongTime();
        if (System.Math.Abs(videoTime - target) > resyncThreshold)
        {
            Debug.Log($"[VideoManager] 싱크 보정: 영상 {videoTime:F2}s → 노래 {target:F2}s");
            SeekTo(target);
        }
    }

    // 지금 노래 위치(초). 영상이 노래보다 짧으면 반복되므로 영상 길이로 나눈 나머지
    double CurrentSongTime()
    {
        double songTime = AudioManager.instance != null ? Mathf.Max(0f, AudioManager.instance.TimeMs) / 1000.0 : 0.0;
        double length = player.length;
        return length > 0 ? songTime % length : songTime;
    }

    void ReleaseTexture()
    {
        if (targetTexture == null) return;
        if (player != null && player.targetTexture == targetTexture) player.targetTexture = null;
        targetTexture.Release();
        Destroy(targetTexture);
        targetTexture = null;
    }
}
