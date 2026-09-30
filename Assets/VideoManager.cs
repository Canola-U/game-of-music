using UnityEngine;
using UnityEngine.Video;
using System.IO;

public class VideoManager : MonoBehaviour
{
    public static VideoManager instance;
    private VideoPlayer player;
    private RenderTexture targetTexture;
    private Transform quadTransform;
    private MeshRenderer quadRenderer;
    private Material quadMaterial;
    public bool videoReady = false;

    private bool hasVideo = false;
    private bool firstFrameLogged = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            player = GetComponent<VideoPlayer>();
            if (player == null) player = gameObject.AddComponent<VideoPlayer>();

            player.playOnAwake = false;
            player.waitForFirstFrame = false;
            player.isLooping = true;
            player.skipOnDrop = true;
            player.source = VideoSource.Url;
            // frameReady 이벤트는 기본값이 false라서 켜주지 않으면 OnFrameReady가 절대 호출되지 않는다
            player.sendFrameReadyEvents = true;

            // 영상에 들어있는 오디오 트랙은 안 쓴다 (오디오는 AudioManager가 따로 재생함).
            // 지금까지 Direct 모드로 오디오까지 디코딩하고 있었는데, 이게 영상 프레임 디코딩과
            // 리소스를 다퉈서 불안정했을 가능성이 있어 확실히 꺼둔다
            player.audioOutputMode = VideoAudioOutputMode.None;

            player.errorReceived += OnVideoError;
            player.prepareCompleted += OnPrepared;
            player.frameReady += OnFrameReady;

            // 스프라이트 대신 Quad를 써서 UV가 항상 텍스처 전체(0~1)를 그대로 사용하게 함
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "VideoQuad";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 5f);
            quad.transform.localRotation = Quaternion.identity;

            quadTransform = quad.transform;
            quadTransform.localPosition = new Vector3(0f, 0f, 5f);
            quadRenderer = quad.GetComponent<MeshRenderer>();
            quadRenderer.enabled = false;

            Debug.Log($"[VideoManager] Quad 생성: name={quad.name}, renderer={quadRenderer}, active={quad.activeInHierarchy}, parent={quad.transform.parent.name}");

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            Debug.Log($"[VideoManager] Shader 찾기: unlitShader={unlitShader}");

            if (unlitShader != null)
            {
                quadMaterial = new Material(unlitShader);
                Debug.Log($"[VideoManager] Material 생성: {quadMaterial}");

                Debug.Log($"[VideoManager] 적용 전: renderer.material={quadRenderer.material}");
                quadRenderer.material = quadMaterial;
                Debug.Log($"[VideoManager] 적용 후: renderer.material={quadRenderer.material}");
            }
            else
            {
                Debug.LogError("[VideoManager] Universal Render Pipeline/Unlit 셰이더를 못 찾았어. 배경 영상 없이 진행");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadVideo(string songFolder)
    {
        videoReady = false;
        hasVideo = false;
        firstFrameLogged = false;
        quadRenderer.enabled = false;
        player.Stop();

        if (targetTexture != null)
        {
            Destroy(targetTexture);
            targetTexture = null;
        }

        string folder = SongMedia.FolderPath(songFolder);
        string filePath = Path.Combine(folder, "video.mp4");

        if (!File.Exists(filePath))
        {
            Debug.Log($"[VideoManager] video.mp4 없음, 배경 없이 진행: {filePath}");
            videoReady = true;
            return;
        }

        // RenderTexture 모드: VideoPlayer가 targetTexture에 직접 렌더링함
        // 이것이 가장 표준적이고 안정적인 방식
        player.renderMode = VideoRenderMode.RenderTexture;

        // 영상 해상도에 맞춰 RenderTexture 생성 (나중에 OnPrepared에서 정확히 설정)
        targetTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        targetTexture.Create();
        player.targetTexture = targetTexture;
        player.url = filePath;

        Debug.Log($"[VideoManager] Prepare 시작: {filePath}");
        player.Prepare();
    }

    void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError($"[VideoManager] errorReceived: {message}");
        if (!videoReady)
        {
            hasVideo = false;
            videoReady = true;
        }
    }

    void OnPrepared(VideoPlayer vp)
    {
        int w = (int)vp.width;
        int h = (int)vp.height;
        Debug.Log($"[VideoManager] Prepare 완료: {w}x{h}, frameCount={vp.frameCount}, frameRate={vp.frameRate}, isPrepared={vp.isPrepared}");

        if (w <= 0 || h <= 0 || quadMaterial == null)
        {
            Debug.LogError("[VideoManager] 배경을 표시할 수 없어서 (영상 크기 이상 또는 셰이더 없음) 배경 없이 진행");
            hasVideo = false;
            videoReady = true;
            return;
        }

        // RenderTexture 크기를 영상 해상도에 맞춰 정확히 설정
        if (targetTexture != null)
        {
            targetTexture.Release();
            Destroy(targetTexture);
        }
        targetTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        targetTexture.Create();
        player.targetTexture = targetTexture;

        // Material에 RenderTexture 할당
        quadMaterial.SetTexture("_BaseMap", targetTexture);

        FitToScreen(w, h);

        quadRenderer.enabled = true;
        hasVideo = true;
        videoReady = true;

        Debug.Log($"[VideoManager] Quad 설정 완료: worldPos={quadTransform.position}, scale={quadTransform.localScale}, renderer.enabled={quadRenderer.enabled}, material={quadRenderer.material.name}");
        Debug.Log($"[VideoManager] RenderTexture: {w}x{h}, assigned to material._BaseMap");
        Debug.Log($"[VideoManager] Camera: pos={Camera.main.transform.position}, orthographic={Camera.main.orthographic}, orthographicSize={Camera.main.orthographicSize}, far={Camera.main.farClipPlane}");
    }

    void OnFrameReady(VideoPlayer vp, long frameIdx)
    {
        if (firstFrameLogged) return;
        firstFrameLogged = true;
        Debug.Log($"[VideoManager] 첫 프레임 도착: frameIdx={frameIdx}, isPlaying={vp.isPlaying}");
    }

    void FitToScreen(int w, int h)
    {
        Camera cam = Camera.main;
        float screenH = cam.orthographicSize * 2f;
        float screenW = screenH * cam.aspect;
        float videoAspect = (float)w / h;
        float screenAspect = screenW / screenH;

        Vector3 scale;
        if (videoAspect > screenAspect)
            scale = new Vector3(screenH * videoAspect, screenH, 1f);
        else
            scale = new Vector3(screenW, screenW / videoAspect, 1f);

        quadTransform.localScale = scale;
        Debug.Log($"[VideoManager] FitToScreen scale={scale}");
    }

    void Update()
    {
        // RenderTexture 모드에서는 VideoPlayer가 자동으로 targetTexture에 렌더링하므로
        // Update에서 별도의 작업이 필요 없음
    }

    public void Play()
    {
        if (hasVideo)
        {
            player.Play();
            Debug.Log($"[VideoManager] Play() 호출, isPlaying={player.isPlaying}, isPrepared={player.isPrepared}");
        }
    }

    public void Pause()
    {
        if (hasVideo) player.Pause();
    }
}
