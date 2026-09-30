using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[System.Serializable]
public class ScoreRecord
{
    public string nickname;
    public float accuracy;
}

[System.Serializable]
class ScoreRecordList
{
    public List<ScoreRecord> records = new List<ScoreRecord>();
}

// 순위 기록은 곡 폴더 안의 records.txt에 저장한다 (map/곡이름/records.txt)
// 한 줄에 하나씩 "정확도<TAB>닉네임", 높은 순서대로 최대 10개
public static class HighScoreManager
{
    const int maxRecords = 10;
    const string fileName = "records.txt";

    static string RecordPath(string songFolder) => Path.Combine(SongMedia.FolderPath(songFolder), fileName);

    public static List<ScoreRecord> GetRecords(string songFolder)
    {
        string path = RecordPath(songFolder);
        if (!File.Exists(path)) return MigrateFromPlayerPrefs(songFolder);

        List<ScoreRecord> records = new List<ScoreRecord>();
        try
        {
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                int tab = line.IndexOf('\t');
                string accText = tab >= 0 ? line.Substring(0, tab) : line;
                string nickname = tab >= 0 ? line.Substring(tab + 1) : "???";

                if (float.TryParse(accText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float accuracy))
                    records.Add(new ScoreRecord { accuracy = accuracy, nickname = nickname.Trim() });
            }
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"[HighScoreManager] 기록을 읽지 못했어요: {path}\n{ex.Message}");
        }

        records.Sort((a, b) => b.accuracy.CompareTo(a.accuracy));
        return records;
    }

    public static void SaveRecord(string songFolder, float accuracy, string nickname)
    {
        List<ScoreRecord> records = GetRecords(songFolder);

        records.Add(new ScoreRecord
        {
            nickname = CleanNickname(nickname),
            accuracy = accuracy
        });

        records.Sort((a, b) => b.accuracy.CompareTo(a.accuracy));

        if (records.Count > maxRecords)
            records.RemoveRange(maxRecords, records.Count - maxRecords);

        WriteRecords(songFolder, records);
    }

    static void WriteRecords(string songFolder, List<ScoreRecord> records)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# 순위 기록 (정확도<TAB>닉네임), 높은 순서");
        foreach (ScoreRecord r in records)
            sb.Append(r.accuracy.ToString("0.###", CultureInfo.InvariantCulture)).Append('\t').AppendLine(r.nickname);

        string path = RecordPath(songFolder);
        try
        {
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
        catch (IOException ex)
        {
            Debug.LogError($"[HighScoreManager] 기록을 저장하지 못했어요: {path}\n{ex.Message}");
        }
    }

    // 탭/줄바꿈이 들어가면 파일 형식이 깨지므로 공백으로 바꾼다
    static string CleanNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return "???";
        return nickname.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    // 예전 버전은 기록을 PlayerPrefs(레지스트리)에 저장했다. 처음 읽을 때 records.txt로 옮긴다
    static List<ScoreRecord> MigrateFromPlayerPrefs(string songFolder)
    {
        string key = "records_" + songFolder;
        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return new List<ScoreRecord>();

        ScoreRecordList list = JsonUtility.FromJson<ScoreRecordList>(json);
        List<ScoreRecord> records = list?.records ?? new List<ScoreRecord>();
        if (records.Count == 0) return records;

        records.Sort((a, b) => b.accuracy.CompareTo(a.accuracy));
        WriteRecords(songFolder, records);
        if (File.Exists(RecordPath(songFolder)))
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Debug.Log($"[HighScoreManager] 예전 기록 {records.Count}개를 {RecordPath(songFolder)}로 옮겼어요.");
        }
        return records;
    }
}
