using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Windows 빌드가 끝나면:
// 1) 기본 곡(StreamingAssets/map)을 exe 옆 map 폴더로 옮긴다 → 유저가 곡을 넣고 뺄 수 있음
//    (이미 webm으로 변환된 mp4는 빼서 용량을 줄인다)
// 2) mp4 자동 변환용 ffmpeg.exe와 라이선스를 exe 옆 Tools 폴더에 넣는다
public class SongBuildPostprocess : IPostprocessBuildWithReport
{
    const string editorMapFolder = "Assets/StreamingAssets/map";

    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        BuildTarget target = report.summary.platform;
        if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64) return;

        string exePath = report.summary.outputPath;
        string buildDir = Path.GetDirectoryName(exePath);
        string dataDir = Path.Combine(buildDir, Path.GetFileNameWithoutExtension(exePath) + "_Data");

        MoveSongs(Path.Combine(dataDir, "StreamingAssets", "map"), Path.Combine(buildDir, "map"));
        CopyFfmpeg(Path.Combine(buildDir, "Tools"));
    }

    static void MoveSongs(string builtMap, string exeMap)
    {
        if (!Directory.Exists(builtMap)) return;

        Directory.CreateDirectory(exeMap);
        int songs = 0;
        foreach (string songDir in Directory.GetDirectories(builtMap))
        {
            // 빌드 폴더에 유저가 넣어둔 곡은 그대로 두고, 기본 곡만 덮어쓴다
            CopyDirectory(songDir, Path.Combine(exeMap, Path.GetFileName(songDir)));
            songs++;
        }
        Directory.Delete(builtMap, true);

        // 에디터에서 이미 webm으로 변환된 mp4는 게임에서 안 쓰므로 뺀다
        int removed = 0;
        long savedBytes = 0;
        if (Directory.Exists(editorMapFolder))
        {
            string editorMap = Path.GetFullPath(editorMapFolder);
            foreach (string mp4 in Directory.GetFiles(editorMap, "*.mp4", SearchOption.AllDirectories))
            {
                if (WebmConversion.NeedsConversion(mp4)) continue;

                string built = Path.Combine(exeMap, mp4.Substring(editorMap.Length).TrimStart('\\', '/'));
                if (!File.Exists(built)) continue;

                savedBytes += new FileInfo(built).Length;
                File.Delete(built);
                removed++;
            }
        }

        Debug.Log($"[빌드] 기본 곡 {songs}개를 exe 옆 map 폴더로 옮겼어요: {exeMap}" +
                  (removed > 0 ? $"\n이미 webm이 있는 mp4 {removed}개를 뺐어요 (-{savedBytes / (1024 * 1024)}MB)" : ""));
    }

    static void CopyFfmpeg(string toolsDir)
    {
        string ffmpeg = VideoAutoConverter.FindFfmpeg();
        if (ffmpeg == null)
        {
            Debug.LogWarning("[빌드] ffmpeg를 찾지 못해서 게임에 넣지 못했어요. 유저가 넣은 mp4는 자동 변환되지 않아요.\n" +
                             "Tools > 영상 > ffmpeg 위치 지정으로 ffmpeg.exe를 골라주세요.");
            return;
        }

        Directory.CreateDirectory(toolsDir);
        string dst = Path.Combine(toolsDir, "ffmpeg.exe");
        if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(ffmpeg).Length)
            File.Copy(ffmpeg, dst, true);

        // ffmpeg는 GPL/LGPL 라이선스라 라이선스 파일과 소스 위치 안내를 같이 배포해야 한다
        string packageDir = Path.GetDirectoryName(Path.GetDirectoryName(ffmpeg));
        CopyIfExists(Path.Combine(packageDir, "LICENSE"), Path.Combine(toolsDir, "FFMPEG-LICENSE.txt"));
        CopyIfExists(Path.Combine(packageDir, "README.txt"), Path.Combine(toolsDir, "FFMPEG-README.txt"));
        File.WriteAllText(Path.Combine(toolsDir, "FFMPEG-NOTICE.txt"),
            "이 게임은 유저가 넣은 mp4 영상을 webm으로 변환하기 위해 FFmpeg(https://ffmpeg.org)를 별도 프로그램으로 포함합니다.\n" +
            "FFmpeg의 라이선스는 FFMPEG-LICENSE.txt, 빌드 정보와 소스 코드 위치는 FFMPEG-README.txt를 참고하세요.\n" +
            "FFmpeg source code: https://ffmpeg.org/download.html\n");

        Debug.Log($"[빌드] ffmpeg를 게임에 넣었어요: {dst} ({new FileInfo(dst).Length / (1024 * 1024)}MB)");
    }

    static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (string file in Directory.GetFiles(src))
        {
            // 개발하면서 쌓인 순위 기록은 게임에 넣지 않고, 빌드 폴더에 이미 있는 유저 기록도 덮어쓰지 않는다
            if (string.Equals(Path.GetFileName(file), "records.txt", System.StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
        }
        foreach (string dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
    }

    static void CopyIfExists(string src, string dst)
    {
        if (File.Exists(src)) File.Copy(src, dst, true);
    }
}
