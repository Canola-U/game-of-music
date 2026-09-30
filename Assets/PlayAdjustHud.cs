using System.Collections;
using UnityEngine;
using TMPro;

// 게임 화면에서 누르고 있으면 연속으로 조절되는 설정 키
//   + / -  : 노트 속도
//   ] / [  : 싱크 보정 (] = 노트가 늦게, [ = 노트가 빠르게)
// 바뀐 값은 화면 위쪽에 잠깐 보여주고, 저장해서 다음에도 유지한다
public class PlayAdjustHud : MonoBehaviour
{
    const float repeatDelay = 0.35f;     // 누르고 있으면 이 시간 뒤부터
    const float repeatInterval = 0.06f;  // 이 간격으로 연속 조절
    const float showTime = 1.2f;
    const float fadeTime = 0.3f;

    enum Setting { None, Speed, Sync }

    private TextMeshProUGUI label;
    private CanvasGroup group;
    private Coroutine fadeRoutine;
    private Setting heldSetting = Setting.None;
    private int heldDirection = 0;
    private float nextRepeat = 0f;

    void Start()
    {
        BuildLabel();
    }

    void Update()
    {
        Setting setting = Setting.None;
        int direction = 0;

        if (Held(KeyCode.Equals, KeyCode.Plus, KeyCode.KeypadPlus)) { setting = Setting.Speed; direction = 1; }
        else if (Held(KeyCode.Minus, KeyCode.KeypadMinus)) { setting = Setting.Speed; direction = -1; }
        else if (Held(KeyCode.RightBracket)) { setting = Setting.Sync; direction = 1; }
        else if (Held(KeyCode.LeftBracket)) { setting = Setting.Sync; direction = -1; }

        if (setting == Setting.None)
        {
            heldSetting = Setting.None;
            heldDirection = 0;
            return;
        }

        if (setting != heldSetting || direction != heldDirection)
        {
            // 처음 누른 순간
            heldSetting = setting;
            heldDirection = direction;
            nextRepeat = Time.unscaledTime + repeatDelay;
            Adjust(setting, direction);
        }
        else if (Time.unscaledTime >= nextRepeat)
        {
            nextRepeat = Time.unscaledTime + repeatInterval;
            Adjust(setting, direction);
        }
    }

    static bool Held(params KeyCode[] keys)
    {
        foreach (KeyCode key in keys)
        {
            if (Input.GetKey(key)) return true;
        }
        return false;
    }

    void Adjust(Setting setting, int direction)
    {
        if (setting == Setting.Speed)
        {
            global g = global.instance;
            g.SetNoteSpeed(g.notespeed + direction * global.noteSpeedStep);
            Show($"노트 속도 {g.notespeed:0.0}", "");
        }
        else
        {
            SyncOffset.Ms += direction * SyncOffset.step;
            string hint = SyncOffset.Ms > 0 ? "노트가 늦게" : (SyncOffset.Ms < 0 ? "노트가 빠르게" : "기본");
            Show($"싱크 {SyncOffset.Label}", hint);
        }
    }

    void Show(string main, string hint)
    {
        if (label == null) return;

        label.text = string.IsNullOrEmpty(hint) ? main : $"{main}   <size=70%><alpha=#99>{hint}</size>";

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(showTime);

        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = 1f - elapsed / fadeTime;
            yield return null;
        }
        group.alpha = 0f;
        fadeRoutine = null;
    }

    void BuildLabel()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        Transform root = canvas.rootCanvas.transform;

        GameObject box = new GameObject("PlayAdjustLabel", typeof(RectTransform));
        box.layer = root.gameObject.layer;
        box.transform.SetParent(root, false);

        RectTransform rect = box.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -40f);
        rect.sizeDelta = new Vector2(600f, 60f);

        group = box.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        label = box.AddComponent<TextMeshProUGUI>();
        label.fontSize = 36;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }
}
