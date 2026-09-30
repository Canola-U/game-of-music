using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 곡 선택 화면의 키 설정 창. 1번 레인부터 차례로 원하는 키를 누르면 바뀌고, 4번까지 누르면 저장된다.
// ESC: 취소 (원래 키로 되돌림)
public class KeyBindPopup : MonoBehaviour
{
    // 게임 중 단축키와 겹치면 안 되는 키
    static readonly KeyCode[] reservedKeys =
    {
        KeyCode.Escape, KeyCode.F5,
        KeyCode.Equals, KeyCode.Plus, KeyCode.KeypadPlus, KeyCode.Minus, KeyCode.KeypadMinus,
        KeyCode.LeftBracket, KeyCode.RightBracket,
    };

    static readonly Color panelColor = new Color(0.106f, 0.106f, 0.141f);
    static readonly Color keyBoxColor = new Color(0.165f, 0.165f, 0.208f);
    static readonly Color activeColor = new Color(1f, 0.835f, 0.31f);
    static readonly Color errorColor = new Color(1f, 0.45f, 0.45f);
    static readonly Color dimText = new Color(1f, 1f, 1f, 0.55f);

    public bool IsOpen { get; private set; }

    // 이 프레임에 창이 닫혔으면 true (닫을 때 누른 ESC가 곡 선택 화면에도 먹히지 않게)
    public bool ClosedThisFrame => closedFrame == Time.frameCount;

    private static KeyCode[] allKeys;

    private GameObject root;
    private Image[] keyBoxes;
    private TextMeshProUGUI[] keyTexts;
    private TextMeshProUGUI guideText;

    private KeyCode[] original;
    private KeyCode[] editing;
    private int currentLane;
    private int openedFrame;
    private int closedFrame = -1;
    private bool finishing;

    int LaneCount => KeyManager.instance.laneKeys.Length;

    public void Open()
    {
        if (IsOpen) return;
        if (root == null) Build();
        if (root == null) return;

        original = (KeyCode[])KeyManager.instance.laneKeys.Clone();
        editing = (KeyCode[])original.Clone();
        currentLane = 0;
        finishing = false;
        openedFrame = Time.frameCount;

        root.SetActive(true);
        root.transform.SetAsLastSibling();
        IsOpen = true;
        Refresh(LaneGuide());
    }

    void Update()
    {
        if (!IsOpen || finishing) return;
        // 창을 연 J 키가 1번 레인에 들어가지 않게, 연 프레임은 건너뛴다
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();   // 저장하지 않고 닫기 = 원래 키 유지
            return;
        }

        KeyCode pressed = PressedKey();
        if (pressed == KeyCode.None) return;

        if (Array.IndexOf(reservedKeys, pressed) >= 0)
        {
            Refresh($"{KeyName(pressed)}는 게임 단축키라서 쓸 수 없어요", true);
            return;
        }

        int usedBy = Array.IndexOf(editing, pressed, 0, currentLane);
        if (usedBy >= 0)
        {
            Refresh($"{KeyName(pressed)}는 이미 {usedBy + 1}번 레인에 쓰고 있어요", true);
            return;
        }

        editing[currentLane] = pressed;
        currentLane++;

        if (currentLane < LaneCount)
        {
            Refresh(LaneGuide());
        }
        else
        {
            for (int i = 0; i < LaneCount; i++) KeyManager.instance.RebindKey(i, editing[i]);
            StartCoroutine(FinishRoutine());
        }
    }

    IEnumerator FinishRoutine()
    {
        finishing = true;
        Refresh("저장했어요!");
        yield return new WaitForSeconds(0.6f);
        Close();
    }

    void Close()
    {
        IsOpen = false;
        finishing = false;
        closedFrame = Time.frameCount;
        if (root != null) root.SetActive(false);
    }

    string LaneGuide() => $"{currentLane + 1}번 레인에 쓸 키를 누르세요";

    static KeyCode PressedKey()
    {
        if (!Input.anyKeyDown) return KeyCode.None;

        if (allKeys == null) allKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        foreach (KeyCode key in allKeys)
        {
            // 마우스/게임패드 버튼은 제외
            if (key == KeyCode.None || key >= KeyCode.Mouse0) continue;
            if (Input.GetKeyDown(key)) return key;
        }
        return KeyCode.None;
    }

    void Refresh(string guide, bool error = false)
    {
        for (int i = 0; i < keyBoxes.Length; i++)
        {
            bool active = !finishing && i == currentLane;
            keyBoxes[i].color = active ? activeColor : keyBoxColor;
            keyTexts[i].text = active ? "?" : KeyName(editing[i]);
            keyTexts[i].color = active ? Color.black : Color.white;
        }
        guideText.text = guide;
        guideText.color = error ? errorColor : Color.white;
    }

    public static string KeyName(KeyCode key)
    {
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
        if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "NUM" + (int)(key - KeyCode.Keypad0);

        switch (key)
        {
            case KeyCode.Space: return "SPACE";
            case KeyCode.Return: return "ENTER";
            case KeyCode.KeypadEnter: return "NUM ENTER";
            case KeyCode.LeftShift: return "L SHIFT";
            case KeyCode.RightShift: return "R SHIFT";
            case KeyCode.LeftControl: return "L CTRL";
            case KeyCode.RightControl: return "R CTRL";
            case KeyCode.LeftAlt: return "L ALT";
            case KeyCode.RightAlt: return "R ALT";
            case KeyCode.Semicolon: return ";";
            case KeyCode.Quote: return "'";
            case KeyCode.Comma: return ",";
            case KeyCode.Period: return ".";
            case KeyCode.Slash: return "/";
            case KeyCode.Backslash: return "\\";
            case KeyCode.BackQuote: return "`";
            case KeyCode.UpArrow: return "UP";
            case KeyCode.DownArrow: return "DOWN";
            case KeyCode.LeftArrow: return "LEFT";
            case KeyCode.RightArrow: return "RIGHT";
            default: return key.ToString().ToUpperInvariant();
        }
    }

    // ───── 화면 구성 ─────

    void Build()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        Transform canvasRoot = canvas.rootCanvas.transform;

        root = new GameObject("KeyBindPopup", typeof(RectTransform));
        root.layer = canvasRoot.gameObject.layer;
        root.transform.SetParent(canvasRoot, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        RectTransform panel = NewRect("Panel", root.transform, Vector2.zero, new Vector2(960f, 480f));
        panel.gameObject.AddComponent<Image>().color = panelColor;

        NewText(panel, "키 설정", 48, Color.white, new Vector2(0f, 180f), new Vector2(900f, 60f));

        int lanes = LaneCount;
        const float boxSize = 150f;
        const float gap = 50f;
        float totalWidth = lanes * boxSize + (lanes - 1) * gap;

        keyBoxes = new Image[lanes];
        keyTexts = new TextMeshProUGUI[lanes];
        for (int i = 0; i < lanes; i++)
        {
            float x = -totalWidth / 2f + boxSize / 2f + i * (boxSize + gap);
            NewText(panel, $"{i + 1}번", 24, dimText, new Vector2(x, 105f), new Vector2(boxSize, 36f));

            RectTransform box = NewRect("Key", panel, new Vector2(x, 10f), new Vector2(boxSize, boxSize));
            keyBoxes[i] = box.gameObject.AddComponent<Image>();
            keyTexts[i] = NewText(box, "", 48, Color.white, Vector2.zero, new Vector2(boxSize, boxSize));
            keyTexts[i].enableAutoSizing = true;
            keyTexts[i].fontSizeMin = 20f;
            keyTexts[i].fontSizeMax = 48f;
        }

        guideText = NewText(panel, "", 30, Color.white, new Vector2(0f, -120f), new Vector2(900f, 44f));
        NewText(panel, "ESC 취소", 24, dimText, new Vector2(0f, -190f), new Vector2(900f, 36f));

        root.SetActive(false);
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static TextMeshProUGUI NewText(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box)
    {
        TextMeshProUGUI t = NewRect("Text", parent, pos, box).gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }
}
