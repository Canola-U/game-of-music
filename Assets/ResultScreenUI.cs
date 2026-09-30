using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Tier = RankTable.Tier;

// 결과 화면 UI를 실행할 때 코드로 만든다.
// 왼쪽: 티어 표 / 가운데: 판정 비율 원 + 이번 판 티어 / 오른쪽: 판정 표
// S: 기록 저장 팝업, F5: 재도전, ENTER/ESC: 곡 선택
public class ResultScreenUI : MonoBehaviour
{
    [Header("비워두면 씬에 있던 기존 텍스트의 폰트를 사용")]
    public TMP_FontAsset font;

    // 티어 이름/커트라인/색은 RankTable.cs에서 바꾼다 (화면 전환과 같이 씀)
    static List<Tier> tiers => RankTable.tiers;

    [Header("판정 색 (원과 표에 같이 쓰임)")]
    public Color perfectColor = Hex(0xFFD54F);
    public Color greatColor = Hex(0x4FC3F7);
    public Color goodColor = Hex(0x81C784);
    public Color missColor = Hex(0xEF5350);

    static readonly Color godColor = RankTable.godColor;
    static readonly Color bgColor = Hex(0x0E0E14);
    static readonly Color panelColor = Hex(0x1B1B24);
    static readonly Color keyColor = Hex(0x2A2A35);
    static readonly Color dimText = new Color(1f, 1f, 1f, 0.55f);

    const float ringSize = 600f;
    const float introDuration = 1.2f;

    float accuracy;
    int score;
    Tier tier;
    bool isNewRecord;
    bool isPerfect;

    readonly List<Image> ringSegments = new List<Image>();
    readonly List<float> ringTargets = new List<float>();
    TextMeshProUGUI tierText;
    TextMeshProUGUI rateText;
    TextMeshProUGUI scoreText;
    GameObject newRecordTag;
    TextMeshProUGUI saveHintLabel;
    TextMeshProUGUI toastText;
    Coroutine toastRoutine;

    GameObject popupRoot;
    TMP_InputField nicknameInput;
    bool popupOpen = false;
    bool confirming = false;
    bool saved = false;
    bool leaving = false;

    void Start()
    {
        if (font == null)
        {
            TextMeshProUGUI old = GetComponentInChildren<TextMeshProUGUI>(true);
            if (old != null) font = old.font;
        }

        // 예전 결과 화면 UI는 숨긴다 (씬에서 지워도 됨)
        foreach (Transform child in transform) child.gameObject.SetActive(false);

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        ScoreManager sm = ScoreManager.instance;
        accuracy = sm.AverageAccuracy;
        score = Mathf.RoundToInt(accuracy * 10000f);
        tier = RankTable.Get(accuracy);
        isPerfect = RankTable.IsGod(accuracy);

        List<ScoreRecord> records = HighScoreManager.GetRecords(SongSelection.songFolder);
        isNewRecord = records.Count == 0 || accuracy > records[0].accuracy;

        BuildBackground();
        BuildHeader();
        BuildTierList();
        BuildRing(sm);
        BuildJudgementPanel(sm);
        BuildHints();
        BuildSavePopup();

        StartCoroutine(PlayIntro());
    }

    // ───────────────────────── 입력 ─────────────────────────

    void Update()
    {
        if (popupOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ClosePopup();
            }
            else if (!confirming && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                StartCoroutine(ConfirmSaveNextFrame());
            }
            return;
        }

        if (leaving) return;

        if (Input.GetKeyDown(KeyCode.S))
        {
            OpenPopup();
        }
        else if (Input.GetKeyDown(KeyCode.F5))
        {
            leaving = true;
            SceneFlow.instance.RetrySong();
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
        {
            leaving = true;
            SceneFlow.instance.GoToSelect();
        }
    }

    void OpenPopup()
    {
        if (saved)
        {
            ShowToast("이미 저장한 기록이에요");
            return;
        }

        popupRoot.SetActive(true);
        popupOpen = true;
        nicknameInput.text = PlayerPrefs.GetString("lastNickname", "");
        StartCoroutine(FocusInputNextFrame());
    }

    // S를 누른 프레임에 바로 포커스를 주면 's'가 입력칸에 같이 들어가서 한 프레임 늦춘다
    IEnumerator FocusInputNextFrame()
    {
        yield return null;
        if (!popupOpen) yield break;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(nicknameInput.gameObject);
        nicknameInput.ActivateInputField();
        nicknameInput.MoveTextEnd(false);
    }

    // 한글 입력 중에 ENTER를 누르면 조합 중인 글자가 다음 프레임에 확정되므로 한 프레임 기다린다
    IEnumerator ConfirmSaveNextFrame()
    {
        confirming = true;
        yield return null;
        confirming = false;
        if (!popupOpen) yield break;

        string nickname = nicknameInput.text.Trim();
        HighScoreManager.SaveRecord(SongSelection.songFolder, accuracy, nickname);
        PlayerPrefs.SetString("lastNickname", nickname);
        PlayerPrefs.Save();

        saved = true;
        saveHintLabel.text = "저장됨";
        saveHintLabel.color = dimText;

        ClosePopup();
        ShowToast("기록을 저장했어요!");
    }

    void ClosePopup()
    {
        nicknameInput.DeactivateInputField();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        popupRoot.SetActive(false);
        popupOpen = false;
    }

    // ───────────────────────── 연출 ─────────────────────────

    IEnumerator PlayIntro()
    {
        float elapsed = 0f;
        while (elapsed < introDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / introDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            for (int i = 0; i < ringSegments.Count; i++)
                ringSegments[i].fillAmount = ringTargets[i] * eased;

            rateText.text = FormatRate(accuracy * eased);
            scoreText.text = FormatScore(Mathf.RoundToInt(score * eased));
            yield return null;
        }

        for (int i = 0; i < ringSegments.Count; i++)
            ringSegments[i].fillAmount = ringTargets[i];
        rateText.text = FormatRate(accuracy);
        scoreText.text = FormatScore(score);

        // 티어가 쾅 하고 들어옴
        tierText.gameObject.SetActive(true);
        const float popDuration = 0.35f;
        elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            tierText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, eased);
            tierText.alpha = t;
            yield return null;
        }
        tierText.rectTransform.localScale = Vector3.one;
        tierText.alpha = 1f;

        if (isNewRecord) newRecordTag.SetActive(true);
    }

    void ShowToast(string message)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(message));
    }

    IEnumerator ToastRoutine(string message)
    {
        toastText.text = message;
        toastText.alpha = 1f;
        yield return new WaitForSecondsRealtime(1.5f);

        float elapsed = 0f;
        while (elapsed < 0.4f)
        {
            elapsed += Time.unscaledDeltaTime;
            toastText.alpha = 1f - elapsed / 0.4f;
            yield return null;
        }
        toastText.alpha = 0f;
    }

    static string FormatRate(float value) => value.ToString("F2", CultureInfo.InvariantCulture) + "%";
    static string FormatScore(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    // ───────────────────────── 화면 구성 ─────────────────────────

    void BuildBackground()
    {
        RectTransform bg = MakeRect("ResultBackground", transform, Vector2.zero, Vector2.zero);
        Stretch(bg);
        bg.gameObject.AddComponent<Image>().color = bgColor;
    }

    void BuildHeader()
    {
        string folder = SongSelection.songFolder;
        SongInfo info = SongInfo.LoadFrom(SongMedia.FolderPath(folder));
        string title = string.IsNullOrEmpty(info.title) ? folder : info.title;

        RectTransform header = MakeRect("Header", transform, new Vector2(0f, -90f), new Vector2(1200f, 140f));
        SetAnchor(header, new Vector2(0.5f, 1f));

        MakeText(header, title, 48, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 20f), new Vector2(1200f, 64f), true);
        if (!string.IsNullOrEmpty(info.difficulty))
            MakeText(header, info.difficulty.ToUpperInvariant(), 26, dimText, TextAlignmentOptions.Center, new Vector2(0f, -32f), new Vector2(1200f, 36f));
    }

    void BuildTierList()
    {
        const float rowHeight = 64f;
        const float rowGap = 10f;
        const float width = 320f;
        float step = rowHeight + rowGap;

        RectTransform list = MakeRect("TierList", transform, new Vector2(width / 2f, 0f), new Vector2(width, step * tiers.Count));
        SetAnchor(list, new Vector2(0f, 0.5f));

        for (int i = 0; i < tiers.Count; i++)
        {
            Tier t = tiers[i];
            bool current = t == tier;
            float y = (tiers.Count - 1) / 2f * step - i * step;

            RectTransform row = MakeRect("Tier " + t.name, list, new Vector2(0f, y), new Vector2(width, rowHeight));

            if (current)
            {
                Image highlight = MakeRect("Highlight", row, Vector2.zero, new Vector2(width, rowHeight)).gameObject.AddComponent<Image>();
                highlight.color = new Color(t.color.r, t.color.g, t.color.b, 0.25f);
            }

            Image bar = MakeRect("Bar", row, new Vector2(-width / 2f + 6f, 0f), new Vector2(12f, rowHeight)).gameObject.AddComponent<Image>();
            bar.color = isPerfect ? godColor : (current ? t.color : new Color(t.color.r, t.color.g, t.color.b, 0.5f));

            string label = isPerfect ? "GOD" : t.name;
            MakeText(row, label, current ? 40 : 32, current ? Color.white : dimText,
                TextAlignmentOptions.Left, new Vector2(-width / 2f + 36f + 100f, 0f), new Vector2(200f, rowHeight), current);

            string range = t.minAccuracy > 0f ? t.minAccuracy.ToString("0.#", CultureInfo.InvariantCulture) + "%+" : "";
            MakeText(row, range, 22, dimText, TextAlignmentOptions.Right,
                new Vector2(width / 2f - 24f - 60f, 0f), new Vector2(120f, rowHeight));
        }
    }

    void BuildRing(ScoreManager sm)
    {
        RectTransform ring = MakeRect("JudgementRing", transform, new Vector2(-260f, -20f), new Vector2(ringSize, ringSize));
        Sprite ringSprite = CreateRingSprite(512, 0.09f);

        Image track = MakeRect("Track", ring, Vector2.zero, new Vector2(ringSize, ringSize)).gameObject.AddComponent<Image>();
        track.sprite = ringSprite;
        track.color = new Color(1f, 1f, 1f, 0.08f);

        // 누적 비율만큼 채운 원을 큰 것부터 깔고, 작은 것을 위에 덮어서 구간별로 색이 나뉘게 한다
        int[] counts = { sm.perfectCount, sm.greatCount, sm.goodCount, sm.missCount };
        Color[] colors = { perfectColor, greatColor, goodColor, missColor };
        int total = sm.perfectCount + sm.greatCount + sm.goodCount + sm.missCount;

        float[] cumulative = new float[counts.Length];
        float sum = 0f;
        for (int i = 0; i < counts.Length; i++)
        {
            sum += counts[i];
            cumulative[i] = total > 0 ? sum / total : 0f;
        }

        for (int i = counts.Length - 1; i >= 0; i--)
        {
            if (counts[i] == 0) continue;

            Image seg = MakeRect("Segment", ring, Vector2.zero, new Vector2(ringSize, ringSize)).gameObject.AddComponent<Image>();
            seg.sprite = ringSprite;
            seg.color = isPerfect ? godColor : colors[i];
            seg.type = Image.Type.Filled;
            seg.fillMethod = Image.FillMethod.Radial360;
            seg.fillOrigin = (int)Image.Origin360.Top;
            seg.fillClockwise = true;
            seg.fillAmount = 0f;

            ringSegments.Add(seg);
            ringTargets.Add(cumulative[i]);
        }

        MakeText(ring, "RANK", 28, dimText, TextAlignmentOptions.Center, new Vector2(0f, 150f), new Vector2(300f, 40f));

        string tierLabel = isPerfect ? "GOD" : tier.name;
        Color tierLabelColor = isPerfect ? godColor : tier.color;
        tierText = MakeText(ring, tierLabel, 200, tierLabelColor, TextAlignmentOptions.Center, new Vector2(0f, -10f), new Vector2(460f, 260f), true);
        tierText.enableAutoSizing = true;
        tierText.fontSizeMin = 60f;
        tierText.fontSizeMax = 200f;
        tierText.gameObject.SetActive(false);
    }

    void BuildJudgementPanel(ScoreManager sm)
    {
        const float width = 640f;
        const float left = -width / 2f;
        RectTransform panel = MakeRect("Judgements", transform, new Vector2(420f, 0f), new Vector2(width, 640f));

        MakeText(panel, "JUDGEMENTS", 28, dimText, TextAlignmentOptions.Left, new Vector2(left + 200f, 270f), new Vector2(400f, 40f));

        MakeText(panel, "MAX COMBO", 36, Color.white, TextAlignmentOptions.Left, new Vector2(left + 150f, 210f), new Vector2(300f, 50f));
        MakeText(panel, sm.maxCombo.ToString(), 40, Color.white, TextAlignmentOptions.Right, new Vector2(0f, 210f), new Vector2(260f, 50f));

        MakeDivider(panel, 165f, width);

        string[] names = { "PERFECT", "GREAT", "GOOD", "MISS" };
        int[] counts = { sm.perfectCount, sm.greatCount, sm.goodCount, sm.missCount };
        Color[] colors = { perfectColor, greatColor, goodColor, missColor };
        int total = counts[0] + counts[1] + counts[2] + counts[3];

        for (int i = 0; i < names.Length; i++)
        {
            float y = 115f - i * 58f;

            Image swatch = MakeRect("Swatch", panel, new Vector2(left + 8f, y), new Vector2(16f, 16f)).gameObject.AddComponent<Image>();
            swatch.color = colors[i];

            MakeText(panel, names[i], 34, Color.white, TextAlignmentOptions.Left, new Vector2(left + 36f + 140f, y), new Vector2(280f, 50f));
            MakeText(panel, counts[i].ToString(), 38, Color.white, TextAlignmentOptions.Right, new Vector2(0f, y), new Vector2(260f, 50f));

            float percent = total > 0 ? counts[i] * 100f / total : 0f;
            MakeText(panel, percent.ToString("F1", CultureInfo.InvariantCulture) + "%", 24, colors[i],
                TextAlignmentOptions.Right, new Vector2(215f, y), new Vector2(170f, 50f));
        }

        MakeDivider(panel, -130f, width);

        MakeText(panel, "RATE", 28, dimText, TextAlignmentOptions.Left, new Vector2(left + 50f, -175f), new Vector2(100f, 40f));
        rateText = MakeText(panel, FormatRate(0f), 64, Color.white, TextAlignmentOptions.Left, new Vector2(left + 170f, -235f), new Vector2(340f, 80f));

        MakeText(panel, "SCORE", 28, dimText, TextAlignmentOptions.Left, new Vector2(170f, -175f), new Vector2(300f, 40f));
        scoreText = MakeText(panel, FormatScore(0), 64, Color.white, TextAlignmentOptions.Left, new Vector2(170f, -235f), new Vector2(300f, 80f));

        RectTransform tag = MakeRect("NewRecord", panel, new Vector2(left + 190f, -175f), new Vector2(170f, 32f));
        tag.gameObject.AddComponent<Image>().color = Hex(0xFFC400);
        MakeText(tag, "NEW RECORD!", 20, Color.black, TextAlignmentOptions.Center, Vector2.zero, new Vector2(170f, 32f), true);
        newRecordTag = tag.gameObject;
        newRecordTag.SetActive(false);
    }

    void BuildHints()
    {
        RectTransform bar = MakeRect("Hints", transform, new Vector2(0f, 60f), new Vector2(1200f, 50f));
        SetAnchor(bar, new Vector2(0.5f, 0f));

        saveHintLabel = MakeKeyHint(bar, "S", "기록 저장", new Vector2(-400f, 0f));
        MakeKeyHint(bar, "F5", "재도전", new Vector2(0f, 0f));
        MakeKeyHint(bar, "ENTER", "곡 선택", new Vector2(400f, 0f));

        toastText = MakeText(transform, "", 30, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 130f), new Vector2(800f, 50f));
        SetAnchor(toastText.rectTransform, new Vector2(0.5f, 0f));
        toastText.alpha = 0f;
    }

    TextMeshProUGUI MakeKeyHint(Transform parent, string key, string label, Vector2 pos)
    {
        RectTransform group = MakeRect("Hint " + key, parent, pos, new Vector2(320f, 50f));

        float keyWidth = 28f + key.Length * 16f;
        RectTransform keyBox = MakeRect("Key", group, new Vector2(-60f - keyWidth / 2f, 0f), new Vector2(keyWidth, 40f));
        keyBox.gameObject.AddComponent<Image>().color = keyColor;
        MakeText(keyBox, key, 22, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(keyWidth, 40f), true);

        return MakeText(group, label, 28, Color.white, TextAlignmentOptions.Left, new Vector2(40f, 0f), new Vector2(180f, 50f));
    }

    void BuildSavePopup()
    {
        RectTransform root = MakeRect("SavePopup", transform, Vector2.zero, Vector2.zero);
        Stretch(root);
        root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        popupRoot = root.gameObject;

        RectTransform panel = MakeRect("Panel", root, Vector2.zero, new Vector2(720f, 380f));
        panel.gameObject.AddComponent<Image>().color = panelColor;

        MakeText(panel, "기록 저장", 44, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 130f), new Vector2(600f, 60f), true);
        MakeText(panel, FormatRate(accuracy) + "  ·  " + tier.name, 30, tier.color, TextAlignmentOptions.Center, new Vector2(0f, 72f), new Vector2(600f, 44f));

        GameObject inputGo = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        inputGo.name = "NicknameInput";
        inputGo.transform.SetParent(panel, false);
        SetLayerRecursive(inputGo.transform, gameObject.layer);

        RectTransform inputRect = inputGo.GetComponent<RectTransform>();
        inputRect.anchoredPosition = new Vector2(0f, -20f);
        inputRect.sizeDelta = new Vector2(520f, 76f);
        inputGo.GetComponent<Image>().color = keyColor;

        nicknameInput = inputGo.GetComponent<TMP_InputField>();
        nicknameInput.lineType = TMP_InputField.LineType.SingleLine;
        nicknameInput.characterLimit = 12;
        if (font != null) nicknameInput.fontAsset = font;
        nicknameInput.pointSize = 34f;
        nicknameInput.customCaretColor = true;
        nicknameInput.caretColor = Color.white;
        nicknameInput.caretWidth = 3;
        nicknameInput.selectionColor = new Color(1f, 1f, 1f, 0.25f);

        nicknameInput.textComponent.color = Color.white;
        nicknameInput.textComponent.alignment = TextAlignmentOptions.Center;

        TMP_Text placeholder = (TMP_Text)nicknameInput.placeholder;
        placeholder.text = "닉네임을 입력하세요";
        placeholder.color = new Color(1f, 1f, 1f, 0.3f);
        placeholder.alignment = TextAlignmentOptions.Center;
        placeholder.fontStyle = FontStyles.Normal;

        MakeText(panel, "ENTER 저장      ESC 취소", 24, dimText, TextAlignmentOptions.Center, new Vector2(0f, -130f), new Vector2(600f, 40f));

        popupRoot.SetActive(false);
    }

    void MakeDivider(Transform parent, float y, float width)
    {
        Image line = MakeRect("Divider", parent, new Vector2(0f, y), new Vector2(width, 2f)).gameObject.AddComponent<Image>();
        line.color = new Color(1f, 1f, 1f, 0.15f);
    }

    // ───────────────────────── 유틸 ─────────────────────────

    RectTransform MakeRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    TextMeshProUGUI MakeText(Transform parent, string text, float size, Color color,
        TextAlignmentOptions align, Vector2 pos, Vector2 box, bool bold = false)
    {
        TextMeshProUGUI t = MakeRect("Text", parent, pos, box).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetAnchor(RectTransform rt, Vector2 anchor)
    {
        rt.anchorMin = rt.anchorMax = anchor;
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayerRecursive(child, layer);
    }

    // 가운데가 뚫린 원(도넛) 스프라이트를 코드로 만든다
    static Sprite CreateRingSprite(int size, float thicknessRatio)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float center = (size - 1) / 2f;
        float outer = size / 2f - 1f;
        float inner = outer * (1f - thicknessRatio);

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Color Hex(uint rgb)
    {
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
