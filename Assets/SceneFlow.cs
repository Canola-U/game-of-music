using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SceneFlow : MonoBehaviour
{
    public static SceneFlow instance;

    [Header("화면 전환")]
    public float transitionDuration = 0.35f;   // 띠 하나가 덮이는(걷히는) 시간
    public float stripStagger = 0.035f;        // 띠끼리 출발 시간 차이
    public float accentLead = 0.06f;           // 강조색 띠가 먼저 지나가는 시간
    public float minHoldTime = 0.3f;           // 화면이 덮인 채로 최소한 머무는 시간 (문구 보여주기)
    public float rankHoldTime = 0.9f;          // 결과 화면으로 갈 때 등급을 보여주는 시간
    public float godHoldTime = 1.5f;           // GOD일 때 보여주는 시간

    const int stripCount = 7;
    const float tiltAngle = 18f;               // 띠 기울기
    const float coverSize = 2600f;             // 기울여도 1920x1080 화면을 다 덮을 만큼 큰 정사각형

    static readonly Color darkColor = new Color(0.055f, 0.055f, 0.08f);
    static readonly Color darkColorAlt = new Color(0.085f, 0.085f, 0.12f);
    static readonly Color goldColor = new Color(1f, 0.835f, 0.31f);

    // 전환 한 번의 모양 (색, 가운데 문구, 연출)
    class Style
    {
        public Color stripA = darkColor;
        public Color stripB = darkColorAlt;
        public Color accent = goldColor;
        public string title = "";
        public string sub = "";
        public Color titleColor = Color.white;
        public Color subColor = goldColor;
        public float titleSize = 48f;
        public float subSize = 24f;
        public bool pop;         // 제목이 크게 쾅 하고 들어옴
        public bool legendary;   // GOD: 글자가 내려앉을 때 빛 "팡!" + 흔들림
        public float holdTime;
    }

    private RectTransform[] accentStrips;
    private RectTransform[] darkStrips;
    private Image[] accentImages;
    private Image[] darkImages;
    private Image flashImage;
    private RectTransform labelRect;
    private CanvasGroup labelGroup;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI subText;
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

    // ───────────────────────── 화면 구성 ─────────────────────────

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
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 기울어진 큰 판 위에 세로 띠들을 나란히 놓으면 사선 띠가 된다
        RectTransform board = NewRect("Strips", canvasObj.transform);
        board.anchorMin = board.anchorMax = new Vector2(0.5f, 0.5f);
        board.sizeDelta = new Vector2(coverSize, coverSize);
        board.localRotation = Quaternion.Euler(0f, 0f, tiltAngle);

        accentStrips = new RectTransform[stripCount];
        darkStrips = new RectTransform[stripCount];
        accentImages = new Image[stripCount];
        darkImages = new Image[stripCount];
        for (int i = 0; i < stripCount; i++)
        {
            accentStrips[i] = NewStrip(board, i, out accentImages[i]);
        }
        for (int i = 0; i < stripCount; i++)
        {
            darkStrips[i] = NewStrip(board, i, out darkImages[i]);
        }

        // GOD 섬광용 흰 화면
        RectTransform flash = NewRect("Flash", canvasObj.transform);
        flash.anchorMin = Vector2.zero;
        flash.anchorMax = Vector2.one;
        flash.offsetMin = flash.offsetMax = Vector2.zero;
        flashImage = flash.gameObject.AddComponent<Image>();
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        flashImage.raycastTarget = false;

        // 가운데 문구 (기울이지 않음)
        labelRect = NewRect("Label", canvasObj.transform);
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(1600f, 400f);
        labelGroup = labelRect.gameObject.AddComponent<CanvasGroup>();
        labelGroup.alpha = 0f;
        labelGroup.blocksRaycasts = false;

        titleText = NewText(labelRect);
        subText = NewText(labelRect);

        SetStrips(accentStrips, 0f, 0f);
        SetStrips(darkStrips, 0f, 0f);
    }

    RectTransform NewStrip(RectTransform board, int index, out Image image)
    {
        RectTransform strip = NewRect("Strip", board);
        strip.anchorMin = new Vector2((float)index / stripCount, 0f);
        strip.anchorMax = new Vector2((float)(index + 1) / stripCount, 1f);

        image = strip.gameObject.AddComponent<Image>();
        image.raycastTarget = true;   // 덮여 있는 동안 클릭 막기 (크기가 0이면 아무것도 안 막음)
        return strip;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static TextMeshProUGUI NewText(RectTransform parent)
    {
        RectTransform rt = NewRect("Text", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    void ApplyStyle(Style style)
    {
        for (int i = 0; i < stripCount; i++)
        {
            darkImages[i].color = i % 2 == 0 ? style.stripA : style.stripB;
            accentImages[i].color = style.accent;
        }

        titleText.text = style.title;
        titleText.fontSize = style.titleSize;
        titleText.color = style.titleColor;
        titleText.rectTransform.sizeDelta = new Vector2(1600f, style.titleSize * 1.4f);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 20f);

        subText.text = style.sub;
        subText.fontSize = style.subSize;
        subText.color = style.subColor;
        subText.rectTransform.sizeDelta = new Vector2(1600f, style.subSize * 1.6f);
        subText.rectTransform.anchoredPosition = new Vector2(0f, 20f - style.titleSize * 0.6f - style.subSize);

        labelRect.localScale = Vector3.one;
        labelRect.anchoredPosition = Vector2.zero;
    }

    // ───────────────────────── 전환 ─────────────────────────

    void GoTo(string sceneName, Style style)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine(sceneName, style));
    }

    IEnumerator TransitionRoutine(string sceneName, Style style)
    {
        isTransitioning = true;
        ApplyStyle(style);

        // 다음 씬은 덮는 동안 뒤에서 미리 불러두고, 다 덮인 뒤에 넘어간다
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        load.allowSceneActivation = false;

        // 왼쪽부터 띠가 차례로 덮음 (강조색이 살짝 먼저)
        yield return AnimateStrips(true);

        float held = 0f;

        if (style.pop)
        {
            yield return PopLabel(style.legendary);
            held += style.legendary ? 0.45f : 0.3f;
        }
        else
        {
            yield return FadeLabel(0f, 1f, 0.15f);
            held += 0.15f;
        }

        float holdTime = Mathf.Max(minHoldTime, style.holdTime);
        while (load.progress < 0.9f || held < holdTime)
        {
            held += Time.unscaledDeltaTime;
            yield return null;
        }

        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;
        yield return null;   // 새 씬의 Start()가 한 번 돌고 나서 보여준다

        yield return FadeLabel(1f, 0f, 0.12f);

        // 왼쪽부터 오른쪽으로 쓸려 나가며 새 화면이 나옴 (강조색이 뒤따라감)
        yield return AnimateStrips(false);

        isTransitioning = false;
    }

    IEnumerator AnimateStrips(bool cover)
    {
        // 덮을 땐 왼쪽을 기준으로 자라고, 걷힐 땐 오른쪽을 기준으로 줄어든다
        float pivotX = cover ? 0f : 1f;
        SetPivot(accentStrips, pivotX);
        SetPivot(darkStrips, pivotX);

        float total = stripStagger * (stripCount - 1) + accentLead + transitionDuration;
        float elapsed = 0f;
        while (elapsed < total)
        {
            elapsed += Time.unscaledDeltaTime;

            if (cover)
            {
                SetStrips(accentStrips, elapsed, 0f);
                SetStrips(darkStrips, elapsed, accentLead);
            }
            else
            {
                SetStrips(darkStrips, elapsed, 0f, true);
                SetStrips(accentStrips, elapsed, accentLead, true);
            }
            yield return null;
        }

        SetStrips(accentStrips, total, 0f, !cover);
        SetStrips(darkStrips, total, 0f, !cover);
    }

    // 띠마다 출발 시간을 조금씩 늦춰서 가로 크기(0~1)를 정한다
    void SetStrips(RectTransform[] strips, float elapsed, float delay, bool shrinking = false)
    {
        for (int i = 0; i < strips.Length; i++)
        {
            float local = (elapsed - delay - i * stripStagger) / transitionDuration;
            float t = EaseInOutCubic(Mathf.Clamp01(local));
            float scale = shrinking ? 1f - t : t;
            strips[i].localScale = new Vector3(scale, 1f, 1f);
        }
    }

    static void SetPivot(RectTransform[] strips, float pivotX)
    {
        foreach (RectTransform strip in strips)
        {
            strip.pivot = new Vector2(pivotX, 0.5f);
            // 이웃 띠와 1px 겹쳐서 틈이 안 보이게
            strip.offsetMin = new Vector2(-1f, 0f);
            strip.offsetMax = new Vector2(1f, 0f);
        }
    }

    IEnumerator FadeLabel(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            labelGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        labelGroup.alpha = to;
    }

    // 등급 글자가 크게 들어오면서 제자리에 튕기듯 멈춤
    // GOD은 글자가 내려앉는 순간 화면이 흔들리고 빛이 "팡!" 한 번 터진다
    IEnumerator PopLabel(bool legendary)
    {
        float duration = legendary ? 0.45f : 0.3f;
        float startScale = legendary ? 2.4f : 1.8f;
        const float impactTime = 0.5f;   // 글자가 제 크기에 처음 닿는 지점 (진행률)
        bool flashed = false;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            labelRect.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, EaseOutBack(t));
            labelGroup.alpha = Mathf.Clamp01(t * 3f);

            if (legendary && t >= impactTime)
            {
                if (!flashed)
                {
                    flashed = true;
                    StartCoroutine(Flash());
                }

                float amplitude = 22f * (1f - t) / (1f - impactTime);
                labelRect.anchoredPosition = Random.insideUnitCircle * amplitude;
            }
            yield return null;
        }

        labelRect.localScale = Vector3.one;
        labelRect.anchoredPosition = Vector2.zero;
        labelGroup.alpha = 1f;
    }

    // 짧고 강하게 번쩍였다가 빠르게 사라지는 빛
    IEnumerator Flash()
    {
        const float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / duration);
            flashImage.color = new Color(1f, 1f, 1f, 0.85f * fade * fade * fade);
            yield return null;
        }
        flashImage.color = new Color(1f, 1f, 1f, 0f);
    }

    static float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ───────────────────────── 외부에서 부르는 함수 ─────────────────────────

    // 전환 중에 또 불리면 무시 (예: 결과 화면으로 넘어가는 중에 ESC → 점수가 초기화되면 안 됨)
    public bool IsTransitioning => isTransitioning;

    public void StartSong(string songFolder)
    {
        if (isTransitioning) return;
        SongSelection.songFolder = songFolder;
        ScoreManager.instance.ResetScore();
        GoToGame();
    }

    public void RetrySong()
    {
        if (isTransitioning) return;
        ScoreManager.instance.ResetScore();
        GoToGame();
    }

    public void GoToSelect()
    {
        if (isTransitioning) return;
        ScoreManager.instance.ResetScore();
        GoTo("selectscreen", new Style { title = "SELECT MUSIC" });
    }

    public void GoToStart()
    {
        if (isTransitioning) return;
        GoTo("startscreen", new Style());
    }

    public void QuitGame()
    {
        if (isTransitioning) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // 에디터에서는 플레이 모드만 끈다
#else
        Application.Quit();
#endif
    }

    public void GoToResult()
    {
        float accuracy = ScoreManager.instance.AverageAccuracy;
        Style style;

        if (RankTable.IsGod(accuracy))
        {
            style = new Style
            {
                // 띠는 전부 같은 빨강 한 가지
                stripA = RankTable.godColor,
                stripB = RankTable.godColor,
                accent = RankTable.godColor,
                title = RankTable.godName,
                sub = "ALL PERFECT",
                titleColor = Color.white,
                subColor = goldColor,
                titleSize = 240f,
                subSize = 48f,
                pop = true,
                legendary = true,
                holdTime = godHoldTime,
            };
        }
        else
        {
            // 등급 색으로 덮고 가운데에 등급 글자
            Color color = RankTable.Get(accuracy).color;
            Color textColor = new Color(0.055f, 0.055f, 0.08f);
            style = new Style
            {
                stripA = color,
                stripB = Color.Lerp(color, Color.black, 0.18f),
                accent = Color.white,
                title = RankTable.NameFor(accuracy),
                sub = "RESULT",
                titleColor = textColor,
                subColor = new Color(textColor.r, textColor.g, textColor.b, 0.7f),
                titleSize = 192f,
                subSize = 36f,
                pop = true,
                holdTime = rankHoldTime,
            };
        }

        GoTo("resultscreen", style);
    }

    void GoToGame()
    {
        // 덮여 있는 동안 곡 제목과 난이도를 보여준다
        SongInfo info = SongInfo.LoadFrom(SongMedia.FolderPath(SongSelection.songFolder));
        GoTo("gamescreen", new Style
        {
            title = string.IsNullOrEmpty(info.title) ? SongSelection.songFolder : info.title,
            sub = string.IsNullOrEmpty(info.difficulty) ? "" : info.difficulty.ToUpperInvariant(),
        });
    }
}
