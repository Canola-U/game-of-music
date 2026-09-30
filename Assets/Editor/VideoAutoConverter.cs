using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 에디터에서 Assets/StreamingAssets/map에 mp4를 넣으면 같은 이름의 webm(VP8, 60fps, 원본 해상도)을 자동으로 만든다.
// (빌드된 게임에서는 SongVideoConverter가 같은 일을 한다. 실제 변환은 둘 다 WebmConversion)
[InitializeOnLoad]
public class VideoAutoConverter : AssetPostprocessor
{
    const string mapFolder = "Assets/StreamingAssets/map";
    const string ffmpegPrefKey = "GameOfMusic.FfmpegPath";

    static readonly Queue<string> queue = new Queue<string>();
    static readonly HashSet<string> queued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static WebmConversion current;
    static int progressId;

    // 에디터를 켜거나 스크립트가 다시 컴파일될 때, 아직 변환 안 된 mp4가 있으면 이어서 변환
    static VideoAutoConverter()
    {
        AssemblyReloadEvents.beforeAssemblyReload += CancelCurrent;
        EditorApplication.quitting += CancelCurrent;
        EditorApplication.delayCall += () => EnqueueAll(false);
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported) TryEnqueue(path, false);
        foreach (string path in moved) TryEnqueue(path, false);
    }

    [MenuItem("Tools/영상/map 폴더의 mp4를 webm으로 변환")]
    static void ConvertAllMenu()
    {
        if (EnqueueAll(false) == 0) Debug.Log("[영상 변환] 새로 변환할 mp4가 없어요. (이미 webm이 최신이에요)");
    }

    [MenuItem("Tools/영상/map 폴더의 mp4를 전부 다시 변환 (webm 덮어쓰기)")]
    static void ReconvertAllMenu()
    {
        if (!EditorUtility.DisplayDialog("영상 다시 변환", "map 폴더의 모든 mp4를 webm으로 다시 변환해요.\n기존 webm은 덮어써요.", "변환", "취소")) return;
        EnqueueAll(true);
    }

    [MenuItem("Tools/영상/ffmpeg 위치 지정")]
    static void SetFfmpegPathMenu()
    {
        string path = EditorUtility.OpenFilePanel("ffmpeg.exe 선택", "", "exe");
        if (string.IsNullOrEmpty(path)) return;
        EditorPrefs.SetString(ffmpegPrefKey, path);
        Debug.Log("[영상 변환] ffmpeg 위치: " + path);
    }

    static int EnqueueAll(bool force)
    {
        if (!Directory.Exists(mapFolder)) return 0;

        int count = 0;
        foreach (string file in Directory.GetFiles(mapFolder, "*.mp4", SearchOption.AllDirectories))
        {
            if (TryEnqueue(file, force)) count++;
        }
        return count;
    }

    static bool TryEnqueue(string assetPath, bool force)
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(mapFolder + "/", StringComparison.OrdinalIgnoreCase)) return false;
        if (!path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) return false;

        string source = Path.GetFullPath(path);
        if (!force && !WebmConversion.NeedsConversion(source)) return false;
        if (!queued.Add(source)) return false;

        queue.Enqueue(source);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        return true;
    }

    static void Tick()
    {
        if (current == null)
        {
            if (queue.Count == 0)
            {
                EditorApplication.update -= Tick;
                return;
            }
            StartJob(queue.Dequeue());
            return;
        }

        if (current.DurationSec > 0)
            Progress.Report(progressId, current.Progress, $"{current.DoneSec:F0} / {current.DurationSec:F0}초");

        if (!current.Poll()) return;

        WebmConversion done = current;
        current = null;
        queued.Remove(done.source);
        Progress.Finish(progressId, done.Succeeded ? Progress.Status.Succeeded : Progress.Status.Failed);

        if (done.Succeeded)
        {
            Debug.Log($"[영상 변환] 완료: {RelativeName(done.output)} ({new FileInfo(done.output).Length / (1024 * 1024)}MB)");
            AssetDatabase.Refresh();
        }
        else
        {
            Debug.LogError($"[영상 변환] 실패: {RelativeName(done.source)}\n{done.LogTail()}");
        }
    }

    static void StartJob(string source)
    {
        string ffmpeg = FindFfmpeg();
        if (ffmpeg == null)
        {
            Debug.LogError("[영상 변환] ffmpeg를 찾을 수 없어요. 설치한 뒤 Tools > 영상 > ffmpeg 위치 지정으로 ffmpeg.exe를 골라주세요.\n" +
                           "설치: winget install --id Gyan.FFmpeg -e");
            queue.Clear();
            queued.Clear();
            return;
        }

        try
        {
            current = WebmConversion.Start(ffmpeg, source, lowPriority: false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[영상 변환] ffmpeg 실행 실패 ({ffmpeg}): {ex.Message}");
            queued.Remove(source);
            return;
        }

        progressId = Progress.Start("webm 변환: " + RelativeName(source), $"VP8 {WebmConversion.targetFps}fps로 변환 중");
        Debug.Log($"[영상 변환] 시작: {RelativeName(source)} → webm ({WebmConversion.targetFps}fps)");
    }

    static void CancelCurrent()
    {
        if (current == null) return;
        current.Cancel();
        Progress.Remove(progressId);
        current = null;
    }

    // 이 PC에 설치된 ffmpeg.exe 찾기 (빌드할 때 게임에 같이 넣을 때도 씀)
    public static string FindFfmpeg()
    {
        string saved = EditorPrefs.GetString(ffmpegPrefKey, "");
        if (File.Exists(saved)) return saved;

        // PATH (방금 설치해서 에디터가 모르는 경우를 위해 시스템/사용자 PATH도 확인)
        string pathVar = string.Join(";",
            Environment.GetEnvironmentVariable("PATH") ?? "",
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "",
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "");
        foreach (string dir in pathVar.Split(';'))
        {
            string trimmed = dir.Trim().Trim('"');
            if (trimmed.Length == 0) continue;
            try
            {
                string candidate = Path.Combine(trimmed, "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }

        // winget으로 설치한 경우
        string winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet");
        string link = Path.Combine(winget, "Links", "ffmpeg.exe");
        if (File.Exists(link)) return link;

        string packages = Path.Combine(winget, "Packages");
        if (Directory.Exists(packages))
        {
            foreach (string dir in Directory.GetDirectories(packages, "*FFmpeg*"))
            {
                string[] found = Directory.GetFiles(dir, "ffmpeg.exe", SearchOption.AllDirectories);
                if (found.Length > 0) return found[0];
            }
        }
        return null;
    }

    static string RelativeName(string fullPath)
    {
        string map = Path.GetFullPath(mapFolder);
        return fullPath.StartsWith(map, StringComparison.OrdinalIgnoreCase)
            ? "map" + fullPath.Substring(map.Length).Replace('\\', '/')
            : Path.GetFileName(fullPath);
    }
}
