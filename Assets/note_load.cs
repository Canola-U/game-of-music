using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class note_data
{
    public string title;
    public string difficulty;
    public int bpm;
    public int start_time;
    public int location;
    public int type;
    public int end_time;
}

public class note_load : MonoBehaviour
{
    public List<note_data> notelist = new List<note_data>();

    string currenttitle;
    string currentdifficulty;
    int currentbpm;

    void Start()
    {
        string file_path = Path.Combine(SongMedia.FolderPath(SongSelection.songFolder), "bitmap.txt");

        if (!File.Exists(file_path))
        {
            Debug.LogError($"파일 없어! 소노 바카!: {file_path}");
            return;
        }

        string[] lines = File.ReadAllLines(file_path);

        foreach (string line in lines)
        {
            if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line) || line.StartsWith("-"))
                continue;

            if (line.StartsWith("title"))
            {
                currenttitle = line.Replace("title", " ").Trim();
                continue;
            }

            if (line.StartsWith("bpm"))
            {
                currentbpm = int.Parse(line.Replace("bpm", "").Trim());
                continue;
            }

            if (line.StartsWith("difficulty"))
            {
                currentdifficulty = line.Replace("difficulty", " ").Trim();
                continue;
            }

            string[] tokens = line.Split(',');
            if (tokens.Length >= 4)
            {
                note_data note = new note_data();

                note.title = currenttitle;
                note.bpm = currentbpm;
                note.difficulty = currentdifficulty;
                note.start_time = int.Parse(tokens[0]);
                note.location = int.Parse(tokens[1]);
                note.type = int.Parse(tokens[2]);
                note.end_time = int.Parse(tokens[3]);

                notelist.Add(note);
            }
        }
    }
}