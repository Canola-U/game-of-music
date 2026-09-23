using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using TMPro;

public class SongSelectUI : MonoBehaviour
{
    public GameObject songButtonPrefab;
    public RectTransform listParent;   // Content 역할
    public float itemHeight = 90f;     // 버튼 하나의 높이 (프리팹 크기와 맞춰주세요)
    public float moveSpeed = 10f;      // 부드럽게 이동하는 속도

    [Header("배경 / 곡 정보")]
    public Image backgroundPanel;
    public Image albumArtPanel;
    public TextMeshProUGUI bigTitleText;
    public TextMeshProUGUI infoText;

    [Header("내 기록 리스트")]
    public RectTransform rankListParent;
    public float rankRowHeight = 26f;

    private List<SongInfo> songs = new List<SongInfo>();
    private List<GameObject> buttons = new List<GameObject>();
    private List<GameObject> rankRows = new List<GameObject>();
    private int selectedIndex = 0;
    private float targetY = 0f;
    private float centerOffset = 0f;

    void Start()
    {
        RectTransform viewport = listParent.parent as RectTransform;
        float viewportHeight = viewport != null ? viewport.rect.height : itemHeight;
        centerOffset = viewportHeight / 2f - itemHeight / 2f;

        LoadSongList();
        BuildUI();
        UpdateSelectionVisual();
        targetY = selectedIndex * itemHeight - centerOffset;
        listParent.anchoredPosition = new Vector2(listParent.anchoredPosition.x, targetY);
    }

    void LoadSongList()
    {
        string mapPath = Path.Combine(Application.streamingAssetsPath, "map");

        if (!Directory.Exists(mapPath))
        {
            Debug.LogError("map 폴더가 없어요: " + mapPath);
            return;
        }

        string[] folders = Directory.GetDirectories(mapPath);

        foreach (string folder in folders)
        {
            SongInfo info = SongInfo.LoadFrom(folder);
            songs.Add(info);
        }
    }

    void BuildUI()
    {
        for (int i = 0; i < songs.Count; i++)
        {
            SongInfo song = songs[i];
            GameObject btn = Instantiate(songButtonPrefab, listParent);

            RectTransform rect = btn.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, -i * itemHeight);

            SetText(btn, "Title", string.IsNullOrEmpty(song.title) ? song.folderName : song.title);
            SetText(btn, "Difficulty", song.difficulty);
            SetText(btn, "Record", BestRecordLabel(song));

            buttons.Add(btn);
        }
    }

    string BestRecordLabel(SongInfo song)
    {
        if (song.records == null || song.records.Count == 0) return "NO RECORD";
        ScoreRecord best = song.records[0];
        return $"{best.accuracy:F1}% - {best.nickname}";
    }

    void SetText(GameObject btn, string childName, string value)
    {
        Transform child = btn.transform.Find(childName);
        if (child == null) return;

        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        if (text != null) text.text = value;
    }

    void Update()
    {
        if (songs.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            selectedIndex = (selectedIndex - 1 + songs.Count) % songs.Count;
            UpdateSelectionVisual();
            targetY = selectedIndex * itemHeight - centerOffset;
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            selectedIndex = (selectedIndex + 1) % songs.Count;
            UpdateSelectionVisual();
            targetY = selectedIndex * itemHeight - centerOffset;
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            SceneFlow.instance.StartSong(songs[selectedIndex].folderName);
        }

        // 목표 위치로 부드럽게 이동
        float currentY = listParent.anchoredPosition.y;
        float newY = Mathf.Lerp(currentY, targetY, moveSpeed * Time.deltaTime);
        listParent.anchoredPosition = new Vector2(listParent.anchoredPosition.x, newY);
    }

    void UpdateSelectionVisual()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            var img = buttons[i].GetComponent<Image>();
            if (img != null)
            {
                img.color = (i == selectedIndex) ? new Color(1f, 0.85f, 0.2f) : Color.white;
            }
        }

        SongInfo selected = songs[selectedIndex];
        Color accent = AccentColorFor(selected.folderName);

        if (backgroundPanel != null) backgroundPanel.color = Color.Lerp(accent, Color.white, 0.55f);
        if (albumArtPanel != null) albumArtPanel.color = accent;
        if (bigTitleText != null) bigTitleText.text = string.IsNullOrEmpty(selected.title) ? selected.folderName : selected.title;
        if (infoText != null) infoText.text = $"{selected.difficulty}   BPM {selected.bpm}";

        RebuildRankList(selected);
    }

    void RebuildRankList(SongInfo song)
    {
        if (rankListParent == null) return;

        foreach (GameObject row in rankRows) Destroy(row);
        rankRows.Clear();

        if (song.records == null || song.records.Count == 0)
        {
            rankRows.Add(CreateRankRow(0, "기록 없음", "", new Color(0.4f, 0.4f, 0.42f)));
            return;
        }

        for (int i = 0; i < song.records.Count; i++)
        {
            ScoreRecord record = song.records[i];
            Color color = i == 0 ? new Color(0.75f, 0.55f, 0.05f) : new Color(0.2f, 0.2f, 0.22f);
            rankRows.Add(CreateRankRow(i, record.nickname, $"{record.accuracy:F1}%", color));
        }
    }

    GameObject CreateRankRow(int index, string nickname, string accuracy, Color color)
    {
        GameObject row = new GameObject("RankRow", typeof(RectTransform));
        row.transform.SetParent(rankListParent, false);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0, 1);
        rowRect.anchorMax = new Vector2(1, 1);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0, rankRowHeight);
        rowRect.anchoredPosition = new Vector2(0, -index * rankRowHeight);

        TextMeshProUGUI nameText = CreateRankText(rowRect, nickname, TextAlignmentOptions.Left);
        nameText.rectTransform.anchorMin = new Vector2(0, 0);
        nameText.rectTransform.anchorMax = new Vector2(0.6f, 1);
        nameText.rectTransform.offsetMin = Vector2.zero;
        nameText.rectTransform.offsetMax = Vector2.zero;
        nameText.color = color;

        TextMeshProUGUI accText = CreateRankText(rowRect, accuracy, TextAlignmentOptions.Right);
        accText.rectTransform.anchorMin = new Vector2(0.6f, 0);
        accText.rectTransform.anchorMax = new Vector2(1, 1);
        accText.rectTransform.offsetMin = Vector2.zero;
        accText.rectTransform.offsetMax = Vector2.zero;
        accText.color = color;

        return row;
    }

    TextMeshProUGUI CreateRankText(RectTransform parent, string text, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 16;
        tmp.alignment = alignment;
        return tmp;
    }

    // 앨범아트가 없을 때, 곡 폴더 이름을 기반으로 항상 같은 색을 만들어줌
    Color AccentColorFor(string folderName)
    {
        int hash = 0;
        foreach (char c in folderName) hash = hash * 31 + c;
        float hue = Mathf.Abs(hash % 360) / 360f;
        return Color.HSVToRGB(hue, 0.45f, 0.95f);
    }
}
