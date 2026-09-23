using UnityEngine;

public class NoteJudge : MonoBehaviour
{
    const float perfectWindow = 50f;
    const float greatWindow = 100f;
    const float goodWindow = 150f;

    void Update()
    {
        if (!GameConductor.gameStarted) return;

        for (int lane = 0; lane < KeyManager.instance.laneKeys.Length; lane++)
        {
            if (Input.GetKeyDown(KeyManager.instance.laneKeys[lane]))
            {
                TryJudge(lane);
            }
        }
    }

    void TryJudge(int lane)
    {
        float songTimeMs = AudioManager.instance.TimeMs;

        notecontroller closestNote = null;
        float closestDiff = float.MaxValue;

        foreach (notecontroller note in notecontroller.activeNotes)
        {
            if (note.noteinfo.location != lane) continue;

            float diff = Mathf.Abs(songTimeMs - note.noteinfo.start_time);

            if (diff < closestDiff)
            {
                closestDiff = diff;
                closestNote = note;
            }
        }

        if (closestNote == null) return;

        if (closestDiff <= perfectWindow)
        {
            Judge("Perfect", closestNote);
        }
        else if (closestDiff <= greatWindow)
        {
            Judge("Great", closestNote);
        }
        else if (closestDiff <= goodWindow)
        {
            Judge("Good", closestNote);
        }
    }

    void Judge(string grade, notecontroller note)
    {
        ScoreManager.instance.RegisterJudge(grade);   // 추가
        Destroy(note.gameObject);
    }
}