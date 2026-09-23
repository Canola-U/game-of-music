using System.Collections.Generic;
using System.IO;

public class SongInfo
{
    public string folderName;   // 폴더 이름 (예: "qna") - 실제 파일 찾을 때 씀
    public string title;        // bitmap.txt에서 읽은 제목
    public string difficulty;
    public int bpm;

    public List<ScoreRecord> records;

    public static SongInfo LoadFrom(string folderPath)
    {
        SongInfo info = new SongInfo();
        info.folderName = Path.GetFileName(folderPath);

        string bitmapPath = Path.Combine(folderPath, "bitmap.txt");
        if (File.Exists(bitmapPath))
        {
            string[] lines = File.ReadAllLines(bitmapPath);
            foreach (string line in lines)
            {
                if (line.StartsWith("title"))
                    info.title = line.Replace("title", " ").Trim();
                else if (line.StartsWith("bpm"))
                    int.TryParse(line.Replace("bpm", "").Trim(), out info.bpm);
                else if (line.StartsWith("difficulty"))
                    info.difficulty = line.Replace("difficulty", " ").Trim();
            }
        }

        info.records = HighScoreManager.GetRecords(info.folderName);

        return info;
    }
}
