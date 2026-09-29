using UnityEngine;
using UnityEngine.SceneManagement;

// 어떤 해상도/모니터에서도 게임 화면을 16:9로 유지하고, 남는 부분은 검은 띠로 채운다.
// 씬에 붙일 필요 없이 게임이 시작되면 자동으로 만들어진다.
public class AspectRatioEnforcer : MonoBehaviour
{
    public const float targetAspect = 16f / 9f;

    // Overlay였던 UI를 카메라 모드로 바꾸면 스프라이트와 같이 정렬되므로, UI가 항상 위에 오도록 올려준다
    const int canvasSortingBoost = 1000;

    Camera barCamera;
    int lastWidth;
    int lastHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        GameObject go = new GameObject("AspectRatioEnforcer");
        DontDestroyOnLoad(go);
        go.AddComponent<AspectRatioEnforcer>();
    }

    void Awake()
    {
        // 16:9 바깥(검은 띠)을 칠해주는 카메라. 아무것도 찍지 않고 검은색으로 지우기만 한다
        barCamera = gameObject.AddComponent<Camera>();
        barCamera.clearFlags = CameraClearFlags.SolidColor;
        barCamera.backgroundColor = Color.black;
        barCamera.cullingMask = 0;
        barCamera.depth = -100;
        barCamera.orthographic = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 씬의 Start()들보다 먼저 호출되므로, UI 위치 계산이 16:9 영역 기준으로 된다
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FitCanvasesToCamera();
        ApplyViewport();
    }

    void Update()
    {
        // 창 크기나 전체화면 전환으로 해상도가 바뀌면 다시 맞춤
        if (Screen.width != lastWidth || Screen.height != lastHeight)
            ApplyViewport();
    }

    void ApplyViewport()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        float windowAspect = (float)Screen.width / Screen.height;
        Rect rect;

        if (windowAspect > targetAspect)
        {
            // 화면이 더 넓음 → 좌우에 검은 띠
            float width = targetAspect / windowAspect;
            rect = new Rect((1f - width) / 2f, 0f, width, 1f);
        }
        else
        {
            // 화면이 더 높음 → 위아래에 검은 띠
            float height = windowAspect / targetAspect;
            rect = new Rect(0f, (1f - height) / 2f, 1f, height);
        }

        foreach (Camera cam in Camera.allCameras)
        {
            if (cam != barCamera) cam.rect = rect;
        }
    }

    // Overlay Canvas는 카메라 영역을 무시하고 화면 전체에 그려지므로 카메라 모드로 바꿔준다
    void FitCanvasesToCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas) continue;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.planeDistance = 1f;
                canvas.sortingOrder += canvasSortingBoost;
                canvas.worldCamera = cam;
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            {
                // 씬을 넘어 살아남은 Canvas는 이전 씬 카메라가 사라졌으므로 새 카메라로 연결
                canvas.worldCamera = cam;
            }
        }
    }
}
