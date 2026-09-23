using System.Collections.Generic;
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

public static class HighScoreManager
{
    const int maxRecords = 10;

    public static List<ScoreRecord> GetRecords(string songFolder)
    {
        string json = PlayerPrefs.GetString("records_" + songFolder, "");
        if (string.IsNullOrEmpty(json)) return new List<ScoreRecord>();

        ScoreRecordList list = JsonUtility.FromJson<ScoreRecordList>(json);
        return list.records ?? new List<ScoreRecord>();
    }

    public static void SaveRecord(string songFolder, float accuracy, string nickname)
    {
        List<ScoreRecord> records = GetRecords(songFolder);

        records.Add(new ScoreRecord
        {
            nickname = string.IsNullOrWhiteSpace(nickname) ? "???" : nickname,
            accuracy = accuracy
        });

        records.Sort((a, b) => b.accuracy.CompareTo(a.accuracy));

        if (records.Count > maxRecords)
            records.RemoveRange(maxRecords, records.Count - maxRecords);

        ScoreRecordList wrapper = new ScoreRecordList { records = records };
        PlayerPrefs.SetString("records_" + songFolder, JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();
    }
}
