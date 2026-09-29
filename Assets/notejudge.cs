using UnityEngine;

public class NoteJudge : MonoBehaviour
{
    public const float perfectWindow = 50f;
    public const float greatWindow = 100f;
    public const float goodWindow = 150f;
    // 이 범위 안에서 너무 일찍 누르면 Miss (빈 입력 연타 방지)
    public const float missWindow = 200f;

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

    // 음수 = 일찍 누름, 양수 = 늦게 누름
    public static string GradeFor(float diffMs)
    {
        float abs = Mathf.Abs(diffMs);
        if (abs <= perfectWindow) return "Perfect";
        if (abs <= greatWindow) return "Great";
        if (abs <= goodWindow) return "Good";
        return "Miss";
    }

    void TryJudge(int lane)
    {
        float songTimeMs = AudioManager.instance.TimeMs;

        // 판정 범위 안에서 가장 먼저 와야 하는 노트를 고른다 (일반 노트 + 롱노트 머리)
        notecontroller tapNote = null;
        longnoteController longNote = null;
        int earliest = int.MaxValue;

        foreach (notecontroller note in notecontroller.activeNotes)
        {
            if (note.judged || note.noteinfo.location != lane) continue;
            if (Mathf.Abs(songTimeMs - note.noteinfo.start_time) > missWindow) continue;

            if (note.noteinfo.start_time < earliest)
            {
                earliest = note.noteinfo.start_time;
                tapNote = note;
                longNote = null;
            }
        }

        foreach (longnoteController note in longnoteController.activeNotes)
        {
            if (note.judged || note.started || note.noteinfo.location != lane) continue;
            if (Mathf.Abs(songTimeMs - note.noteinfo.start_time) > missWindow) continue;

            if (note.noteinfo.start_time < earliest)
            {
                earliest = note.noteinfo.start_time;
                longNote = note;
                tapNote = null;
            }
        }

        // 근처에 노트가 없으면 아무 일도 없음
        if (earliest == int.MaxValue) return;

        string grade = GradeFor(songTimeMs - earliest);

        if (tapNote != null)
        {
            tapNote.judged = true;
            ScoreManager.instance.RegisterJudge(grade);
            Destroy(tapNote.gameObject);
        }
        else
        {
            longNote.StartHold(grade);
        }
    }
}
