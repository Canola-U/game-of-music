using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFlow : MonoBehaviour
{
    public static SceneFlow instance;

    public float transitionDuration = 0.35f;

    // 커튼을 완전히 화면 밖으로 치워둘 때 쓰는 오프셋 (레퍼런스 해상도 1080보다 넉넉히 큼)
    const float offScreenY = 1400f;

    private RectTransform curtainRect;
    private bool isTransitioning = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildCurtain();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void BuildCurtain()
    {
        GameObject canvasObj = new GameObject("TransitionCanvas");
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasObj.AddComponent<KeepScreenSpaceOverlay>();

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject curtainObj = new GameObject("Curtain");
        curtainObj.transform.SetParent(canvasObj.transform, false);

        Image image = curtainObj.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        curtainRect = curtainObj.GetComponent<RectTransform>();
        curtainRect.anchorMin = Vector2.zero;
        curtainRect.anchorMax = Vector2.one;
        curtainRect.offsetMin = Vector2.zero;
        curtainRect.offsetMax = Vector2.zero;

        SetCurtainY(-offScreenY);
    }

    void SetCurtainY(float y)
    {
        curtainRect.anchoredPosition = new Vector2(0f, y);
    }

    void GoTo(string sceneName)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine(sceneName));
    }

    IEnumerator TransitionRoutine(string sceneName)
    {
        isTransitioning = true;

        // 아래에서 검은 사각형이 올라와 화면을 가림
        yield return AnimateCurtain(-offScreenY, 0f);

        SceneManager.LoadScene(sceneName);
        yield return null;

        // 다시 아래로 내려가며 새 화면을 보여줌
        yield return AnimateCurtain(0f, -offScreenY);

        isTransitioning = false;
    }

    IEnumerator AnimateCurtain(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            SetCurtainY(Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetCurtainY(to);
    }

    public void StartSong(string songFolder)
    {
        SongSelection.songFolder = songFolder;
        ScoreManager.instance.ResetScore();
        GoTo("gamescreen");
    }

    public void RetrySong()
    {
        ScoreManager.instance.ResetScore();
        GoTo("gamescreen");
    }

    public void GoToSelect()
    {
        ScoreManager.instance.ResetScore();
        GoTo("selectscreen");
    }

    public void GoToResult()
    {
        GoTo("resultscreen");
    }
}
