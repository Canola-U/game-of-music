using UnityEngine;
using UnityEngine.Video;
using System.IO;

public class VideoManager : MonoBehaviour
{
    public static VideoManager instance;
    private VideoPlayer player;
    private Transform quadTransform;
    private MeshRenderer quadRenderer;
    private Material quadMaterial;
    public bool videoReady = false;
    private bool hasVideo = false;
    private Texture2D imageTexture;   // 영상 대신 사진을 배경으로 쓸 때

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            player = GetComponent<VideoPlayer>();
            player.errorReceived += OnVideoError;
            player.renderMode = VideoRenderMode.APIOnly;

            // 스프라이트 대신 Quad를 써서 UV가 항상 텍스처 전체(0~1)를 그대로 사용하게 함
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "VideoQuad";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 5f);
            quad.transform.localRotation = Quaternion.identity;

            quadTransform = quad.transform;
            quadRenderer = quad.GetComponent<MeshRenderer>();

            quadMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            quadRenderer.material = quadMaterial;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadVideo(string songFolder)
    {
        videoReady = false;
        player.Stop();

        ReleaseImage();

        string folder = SongMedia.FolderPath(songFolder);
        string filePath = Path.Combine(folder, "video.mp4");

        if (!File.Exists(filePath))
        {
            // 영상 대신 사진이 있으면 사진을 배경으로 깐다
            string imagePath = SongMedia.FindImage(folder, "bg");
            if (imagePath != null && ShowImage(imagePath)) return;

            // 둘 다 없으면 배경 없이 바로 시작할 수 있게 준비 완료 처리
            Debug.Log($"[VideoManager] 영상/사진 없음, 배경 없이 진행: {folder}");
            SetNoVideo();
            return;
        }

        hasVideo = true;
        lastBoundTexture = null;
        quadRenderer.enabled = true;
        player.url = filePath;
        player.isLooping = true;

        player.prepareCompleted -= OnPrepared;
        player.prepareCompleted += OnPrepared;

        Debug.Log($"[VideoManager] LoadVideo url={filePath} (exists={File.Exists(filePath)})");
        player.Prepare();
    }

    void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError($"[VideoManager] errorReceived: {message}");

        // 영상이 깨져 있어도 게임은 멈추지 않고 배경 없이 진행
        if (!videoReady) SetNoVideo();
    }

    bool ShowImage(string imagePath)
    {
        imageTexture = SongMedia.LoadImage(imagePath);
        if (imageTexture == null) return false;

        hasVideo = false;
        player.Stop();
        lastBoundTexture = null;
        quadMaterial.mainTexture = imageTexture;
        FitToScreen(imageTexture.width, imageTexture.height);
        quadRenderer.enabled = true;
        videoReady = true;
        Debug.Log($"[VideoManager] 사진 배경 사용: {imagePath}");
        return true;
    }

    void ReleaseImage()
    {
        if (imageTexture == null) return;
        if (quadMaterial.mainTexture == imageTexture) quadMaterial.mainTexture = null;
        Destroy(imageTexture);
        imageTexture = null;
    }

    void SetNoVideo()
    {
        hasVideo = false;
        player.Stop();
        quadRenderer.enabled = false;
        videoReady = true;
    }

    void OnPrepared(VideoPlayer vp)
    {
        Debug.Log($"[VideoManager] OnPrepared, clip size={vp.width}x{vp.height}");
        FitToScreen((int)vp.width, (int)vp.height);
        videoReady = true;
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

    Texture lastBoundTexture = null;
    void Update()
    {
        // VideoPlayer가 내부적으로 들고 있는 디코딩 텍스처를 매 프레임 그대로 따라가서 붙임
        // (백엔드에 따라 텍스처 객체 자체가 바뀔 수 있어서 계속 확인)
        // 사진 배경일 때는 영상 텍스처로 덮어쓰지 않음
        if (hasVideo && player != null && player.texture != null && player.texture != lastBoundTexture)
        {
            lastBoundTexture = player.texture;
            quadMaterial.mainTexture = lastBoundTexture;
            Debug.Log($"[VideoManager] rebound texture={lastBoundTexture} ({lastBoundTexture.width}x{lastBoundTexture.height})");
        }
    }

    public void Play()
    {
        if (!hasVideo) return;
        player.Play();
        Debug.Log($"[VideoManager] Play() called, isPlaying={player.isPlaying}, texture={player.texture}");
    }

    public void Pause()
    {
        if (hasVideo) player.Pause();
    }
}
