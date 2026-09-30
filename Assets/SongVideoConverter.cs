using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 빌드된 게임에서, 유저가 map 폴더에 넣은 mp4를 webm으로 자동 변환한다.
// ffmpeg는 빌드할 때 exe 옆 Tools/ffmpeg.exe로 같이 들어간다 (SongBuildPostprocess).
// 에디터에서는 VideoAutoConverter(에디터 스크립트)가 대신 변환하므로 여기서는 아무것도 안 한다.
public class SongVideoConverter : MonoBehaviour
{
    public static SongVideoConverter instance;

    // 어떤 곡의 영상 변환이 끝났을 때 (곡 폴더 이름)
    public static event Action<string> Converted;

    private readonly Queue<string> queue = new Queue<string>();
    private readonly HashSet<string> queued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private WebmConversion current;
    private string ffmpegPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Application.isEditor || instance != null) return;

        GameObject go = new GameObject("SongVideoConverter");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SongVideoConverter>();
    }

    public static string FfmpegPath =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath), "Tools", "ffmpeg.exe");

    void Awake()
    {
        ffmpegPath = FfmpegPath;
        if (!File.Exists(ffmpegPath))
        {
            Debug.LogWarning("[SongVideoConverter] ffmpeg가 없어서 mp4 자동 변환을 못 해요: " + ffmpegPath);
            ffmpegPath = null;
        }
    }

    void Start()
    {
        Scan();
    }

    // map 폴더를 훑어서 변환이 필요한 mp4를 대기열에 넣는다 (곡 선택 화면에 들어올 때마다 다시 불러도 됨)
    public void Scan()
    {
        if (ffmpegPath == null) return;

        string map = SongMedia.MapRoot;
        if (!Directory.Exists(map)) return;

        foreach (string mp4 in Directory.GetFiles(map, "*.mp4", SearchOption.AllDirectories))
        {
            if (!WebmConversion.NeedsConversion(mp4)) continue;
            if (current != null && string.Equals(current.source, mp4, StringComparison.OrdinalIgnoreCase)) continue;
            if (queued.Add(mp4)) queue.Enqueue(mp4);
        }
    }

    // 이 곡의 영상이 변환 중(또는 대기 중)인지, 진행률은 얼마인지
    public bool IsConverting(string songFolder, out float progress)
    {
        progress = 0f;
        string folder = Path.GetFullPath(SongMedia.FolderPath(songFolder));

        if (current != null && IsInFolder(current.source, folder))
        {
            progress = current.Progress;
            return true;
        }
        foreach (string path in queued)
        {
            if (IsInFolder(path, folder)) return true;
        }
        return false;
    }

    public int PendingCount => queue.Count + (current != null ? 1 : 0);

    void Update()
    {
        if (current == null)
        {
            if (queue.Count == 0) return;

            string next = queue.Dequeue();
            queued.Remove(next);
            if (!File.Exists(next)) return;

            try
            {
                current = WebmConversion.Start(ffmpegPath, next, lowPriority: true);
                Debug.Log("[SongVideoConverter] 변환 시작: " + next);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SongVideoConverter] ffmpeg 실행 실패: {ex.Message}");
                current = null;
            }
            return;
        }

        if (!current.Poll()) return;

        WebmConversion done = current;
        current = null;

        if (done.Succeeded)
        {
            Debug.Log("[SongVideoConverter] 변환 완료: " + done.output);
            string songFolder = Path.GetFileName(Path.GetDirectoryName(done.source));
            Converted?.Invoke(songFolder);
        }
        else
        {
            Debug.LogError($"[SongVideoConverter] 변환 실패: {done.source}\n{done.LogTail()}");
        }
    }

    void OnApplicationQuit()
    {
        // 게임을 끄면 변환도 멈춘다 (다음에 켜면 처음부터 다시 변환)
        current?.Cancel();
        current = null;
    }

    static bool IsInFolder(string filePath, string folder)
    {
        return string.Equals(Path.GetDirectoryName(Path.GetFullPath(filePath)), folder, StringComparison.OrdinalIgnoreCase);
    }
}
