using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;
using System.Collections;
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

    [Header("하이라이트 미리보기 (곡 폴더의 preview.mp4 / preview.wav)")]
    public float previewDelay = 0.3f;     // 선택이 이 시간(초) 동안 멈춰 있어야 불러옴
    public float previewVolume = 0.8f;
    public float previewFadeIn = 0.5f;

    private VideoPlayer previewPlayer;
    private AudioSource previewAudio;
    private AudioClip previewClip;
    private RenderTexture previewTexture;
    private Texture2D previewPhoto;   // 영상 대신 사진을 보여줄 때
    private RawImage previewImage;
    private Coroutine previewRoutine;
    private bool videoPrepared;
    private bool videoFailed;

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

        SetupPreview();
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
        RequestPreview(selected);
    }

    // ───── 하이라이트 미리보기 ─────

    void SetupPreview()
    {
        previewAudio = gameObject.AddComponent<AudioSource>();
        previewAudio.playOnAwake = false;
        previewAudio.loop = true;

        if (albumArtPanel == null) return;

        previewPlayer = gameObject.AddComponent<VideoPlayer>();
        previewPlayer.playOnAwake = false;
        previewPlayer.isLooping = true;
        previewPlayer.skipOnDrop = true;
        previewPlayer.renderMode = VideoRenderMode.RenderTexture;
        previewPlayer.audioOutputMode = VideoAudioOutputMode.None;   // 소리는 preview.wav로 따로 튼다
        previewPlayer.prepareCompleted += OnPreviewPrepared;
        previewPlayer.errorReceived += (vp, message) =>
        {
            Debug.LogWarning("[SongSelectUI] 미리보기 영상 오류: " + message);
            videoFailed = true;
        };

        // 앨범아트 위에 꽉 차게 영상을 덮는다 (영상이 없으면 숨겨서 원래 색 앨범아트가 보임)
        GameObject go = new GameObject("VideoPreview", typeof(RectTransform));
        go.layer = albumArtPanel.gameObject.layer;
        go.transform.SetParent(albumArtPanel.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        previewImage = go.AddComponent<RawImage>();
        previewImage.raycastTarget = false;
        previewImage.enabled = false;
    }

    void RequestPreview(SongInfo song)
    {
        StopPreview();
        previewRoutine = StartCoroutine(PreviewRoutine(song));
    }

    IEnumerator PreviewRoutine(SongInfo song)
    {
        // 방향키로 빠르게 넘길 때마다 큰 파일을 불러오지 않도록 잠깐 기다린다
        yield return new WaitForSeconds(previewDelay);

        string folder = SongMedia.FolderPath(song.folderName);

        // 보여줄 화면: 하이라이트 영상 → 하이라이트 사진 → 전체 영상 → 배경 사진 순서로 찾는다
        string visualPath = null;
        if (previewImage != null)
        {
            visualPath = SongMedia.FirstExisting(folder, "preview.mp4")
                ?? SongMedia.FindImage(folder, "preview")
                ?? SongMedia.FirstExisting(folder, "video.mp4")
                ?? SongMedia.FindImage(folder, "bg");
        }
        bool isImage = visualPath != null && SongMedia.IsImage(visualPath);
        string videoPath = isImage ? null : visualPath;

        string audioPath = SongMedia.FirstExisting(folder, "preview.wav", "preview.ogg", "preview.mp3");
        if (visualPath == null && audioPath == null) yield break;

        // 영상 준비와 노래 로드를 동시에 시작
        if (videoPath != null)
        {
            previewPlayer.url = videoPath;
            previewPlayer.Prepare();
        }

        if (audioPath != null)
        {
            string url = new System.Uri(audioPath).AbsoluteUri;
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioTypeFor(audioPath)))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                    previewClip = DownloadHandlerAudioClip.GetContent(www);
                else
                    Debug.LogWarning("[SongSelectUI] 하이라이트 노래 로드 실패: " + www.error);
            }
        }

        // 영상도 준비될 때까지 기다렸다가 노래와 같이 시작 (최대 5초)
        float waited = 0f;
        while (videoPath != null && !videoPrepared && !videoFailed && waited < 5f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        bool playVideo = videoPrepared && !videoFailed;
        if (playVideo)
        {
            previewPlayer.Play();
            previewImage.enabled = true;
        }
        else if (isImage)
        {
            previewPhoto = SongMedia.LoadImage(visualPath);
            if (previewPhoto != null)
            {
                previewImage.texture = previewPhoto;
                FillAlbumArt((float)previewPhoto.width / previewPhoto.height);
                previewImage.enabled = true;
            }
        }

        if (previewClip == null) yield break;

        previewAudio.clip = previewClip;
        previewAudio.volume = 0f;
        previewAudio.Play();

        float lastAudioTime = 0f;
        float fade = 0f;
        while (true)
        {
            if (fade < previewFadeIn)
            {
                fade += Time.deltaTime;
                previewAudio.volume = previewVolume * Mathf.Clamp01(fade / previewFadeIn);
            }

            // 노래가 처음으로 돌아가면 영상도 처음으로 맞춰서 반복해도 싱크가 안 밀리게
            if (playVideo && previewAudio.time < lastAudioTime)
                previewPlayer.time = 0;
            lastAudioTime = previewAudio.time;

            yield return null;
        }
    }

    static AudioType AudioTypeFor(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".ogg": return AudioType.OGGVORBIS;
            case ".mp3": return AudioType.MPEG;
            default: return AudioType.WAV;
        }
    }

    void OnPreviewPrepared(VideoPlayer vp)
    {
        if (previewRoutine == null) return;   // 그사이 다른 곡으로 넘어가서 취소된 경우

        // 영상 비율에 맞는 텍스처를 만들어서 앨범아트에 꽉 차게 보여준다
        float videoAspect = vp.height > 0 ? (float)vp.width / vp.height : 16f / 9f;
        ReleasePreviewTexture();
        previewTexture = new RenderTexture(640, Mathf.Max(1, Mathf.RoundToInt(640f / videoAspect)), 0);
        vp.targetTexture = previewTexture;
        previewImage.texture = previewTexture;
        FillAlbumArt(videoAspect);

        videoPrepared = true;   // 실제 재생은 PreviewRoutine에서 노래와 같이 시작
    }

    // 영상/사진이 찌그러지지 않고 앨범아트 칸을 꽉 채우도록 가운데를 잘라서 보여준다
    void FillAlbumArt(float contentAspect)
    {
        Rect panel = previewImage.rectTransform.rect;
        float panelAspect = panel.height > 0f ? panel.width / panel.height : 1f;
        if (contentAspect > panelAspect)
        {
            float w = panelAspect / contentAspect;
            previewImage.uvRect = new Rect((1f - w) / 2f, 0f, w, 1f);
        }
        else
        {
            float h = contentAspect / panelAspect;
            previewImage.uvRect = new Rect(0f, (1f - h) / 2f, 1f, h);
        }
    }

    void StopPreview()
    {
        if (previewRoutine != null) StopCoroutine(previewRoutine);
        previewRoutine = null;
        videoPrepared = false;
        videoFailed = false;

        if (previewPlayer != null) previewPlayer.Stop();
        if (previewImage != null) previewImage.enabled = false;

        previewAudio.Stop();
        previewAudio.clip = null;
        ReleasePreviewClip();
        ReleasePreviewPhoto();
    }

    void ReleasePreviewPhoto()
    {
        if (previewPhoto == null) return;
        if (previewImage != null && previewImage.texture == previewPhoto) previewImage.texture = null;
        Destroy(previewPhoto);
        previewPhoto = null;
    }

    void ReleasePreviewClip()
    {
        if (previewClip == null) return;
        Destroy(previewClip);
        previewClip = null;
    }

    void ReleasePreviewTexture()
    {
        if (previewTexture == null) return;
        if (previewPlayer != null) previewPlayer.targetTexture = null;
        previewTexture.Release();
        Destroy(previewTexture);
        previewTexture = null;
    }

    void OnDestroy()
    {
        ReleasePreviewTexture();
        ReleasePreviewClip();
        ReleasePreviewPhoto();
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
