using System.IO;
using UnityEngine;

// 곡 폴더(map/곡이름)에서 영상/사진 파일을 찾고 불러오는 공용 함수들
public static class SongMedia
{
    public static readonly string[] imageExtensions = { ".png", ".jpg", ".jpeg" };

    // 곡들이 들어있는 map 폴더.
    // 빌드된 게임: exe 옆의 map 폴더 (유저가 곡을 넣고 뺄 수 있음)
    // 에디터: Assets/StreamingAssets/map
    public static string MapRoot
    {
        get
        {
            if (Application.isEditor) return Path.Combine(Application.streamingAssetsPath, "map");

            // 유저가 map 폴더를 지웠어도 다시 만들어서, 새 맵을 넣을 자리가 항상 있게 한다
            string map = Path.Combine(Path.GetDirectoryName(Application.dataPath), "map");
            Directory.CreateDirectory(map);
            return map;
        }
    }

    public static string FolderPath(string songFolder)
    {
        return Path.Combine(MapRoot, songFolder);
    }

    // names 순서대로 찾아서 처음 존재하는 파일 경로를 돌려준다 (없으면 null)
    public static string FirstExisting(string folder, params string[] names)
    {
        foreach (string name in names)
        {
            string path = Path.Combine(folder, name);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    // 재생은 .webm(VP8)만 한다. VP8은 Unity가 자체 디코더로 풀어서 어디서나 재생되지만,
    // .mp4(H.264)는 Windows 디코더를 거쳐서 PC에 따라 첫 프레임에서 멈춘다.
    // .mp4는 같은 이름의 .webm으로 자동 변환된다 (에디터: VideoAutoConverter, 게임: SongVideoConverter)
    // "video" → video.webm (없으면 null)
    public static string FindVideo(string folder, string baseName)
    {
        string path = Path.Combine(folder, baseName + ".webm");
        return File.Exists(path) ? path : null;
    }

    // "bg" → bg.png / bg.jpg / bg.jpeg 중 있는 것
    public static string FindImage(string folder, string baseName)
    {
        foreach (string ext in imageExtensions)
        {
            string path = Path.Combine(folder, baseName + ext);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    public static bool IsImage(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return System.Array.IndexOf(imageExtensions, ext) >= 0;
    }

    // 사진 파일을 텍스처로 불러온다. 다 쓰면 Object.Destroy로 지워줘야 함
    public static Texture2D LoadImage(string path)
    {
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(File.ReadAllBytes(path)))
        {
            Debug.LogWarning("[SongMedia] 사진을 읽을 수 없어요: " + path);
            Object.Destroy(tex);
            return null;
        }
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }
}
