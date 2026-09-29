using System.IO;
using UnityEngine;

// 곡 폴더(StreamingAssets/map/곡이름)에서 영상/사진 파일을 찾고 불러오는 공용 함수들
public static class SongMedia
{
    public static readonly string[] imageExtensions = { ".png", ".jpg", ".jpeg" };

    public static string FolderPath(string songFolder)
    {
        return Path.Combine(Application.streamingAssetsPath, "map", songFolder);
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
