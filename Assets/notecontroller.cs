using System.Collections.Generic;
using UnityEngine;

public class notecontroller : MonoBehaviour
{
    public note_data noteinfo;
    public bool judged = false;

    public static List<notecontroller> activeNotes = new List<notecontroller>();

    public void Init(note_data data)
    {
        noteinfo = data;
        UpdatePosition();
    }

    void OnEnable()
    {
        activeNotes.Add(this);
    }

    void OnDisable()
    {
        activeNotes.Remove(this);
    }

    void Update()
    {
        UpdatePosition();

        // 늦게 지나간 노트는 시간 기준으로 Miss
        if (!judged && AudioManager.instance.TimeMs - noteinfo.start_time > NoteJudge.goodWindow)
        {
            judged = true;
            ScoreManager.instance.RegisterJudge("Miss");
            Destroy(gameObject);
        }
    }

    // 프레임 시간이 아니라 음악 시간으로 위치를 계산해서 싱크가 밀리지 않게 한다
    void UpdatePosition()
    {
        float remainMs = noteinfo.start_time - AudioManager.instance.TimeMs;
        Vector3 pos = transform.position;
        pos.y = global.instance.judgelineY + remainMs / 1000f * global.instance.notespeed;
        transform.position = pos;
    }
}
