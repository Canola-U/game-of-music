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

        string filePath = Path.Combine(Application.streamingAssetsPath, "map", songFolder, "video.mp4");
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
        if (player != null && player.texture != null && player.texture != lastBoundTexture)
        {
            lastBoundTexture = player.texture;
            quadMaterial.mainTexture = lastBoundTexture;
            Debug.Log($"[VideoManager] rebound texture={lastBoundTexture} ({lastBoundTexture.width}x{lastBoundTexture.height})");
        }
    }

    public void Play()
    {
        player.Play();
        Debug.Log($"[VideoManager] Play() called, isPlaying={player.isPlaying}, texture={player.texture}");
    }
    public void Pause() => player.Pause();
}
