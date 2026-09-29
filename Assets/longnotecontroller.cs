using System.Collections.Generic;
using UnityEngine;

public class longnoteController : MonoBehaviour
{
    public note_data noteinfo;
    public Transform body;

    public bool started = false;
    public bool judged = false;
    private string headGrade;

    // 끝나기 이 시간(ms) 안에 떼면 끝까지 누른 걸로 인정
    const float releaseWindow = 100f;

    public static List<longnoteController> activeNotes = new List<longnoteController>();

    private float baseHeight;
    private SpriteRenderer bodySr;

    public void Init(note_data data)
    {
        noteinfo = data;

        float durationSec = (data.end_time - data.start_time) / 1000f;
        float fullLength = durationSec * global.instance.notespeed;

        if (body != null)
        {
            bodySr = body.GetComponent<SpriteRenderer>();
            baseHeight = bodySr.sprite.bounds.size.y;

            Vector3 scale = body.localScale;
            scale.y = fullLength / baseHeight;
            body.localScale = scale;

            body.localPosition = Vector3.zero;
            bodySr.maskInteraction = SpriteMaskInteraction.None;
        }

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

    // NoteJudge가 이 레인의 키가 "눌린 순간"에 호출한다
    public void StartHold(string grade)
    {
        if (grade == "Miss")
        {
            Finish("Miss");
            return;
        }

        started = true;
        headGrade = grade;
        bodySr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
    }

    void Update()
    {
        if (judged) return;

        UpdatePosition();

        float songTimeMs = AudioManager.instance.TimeMs;

        if (!started)
        {
            // 머리를 놓침
            if (songTimeMs - noteinfo.start_time > NoteJudge.goodWindow)
            {
                Finish("Miss");
            }
            return;
        }

        if (songTimeMs >= noteinfo.end_time)
        {
            Finish(headGrade);
            return;
        }

        KeyCode key = KeyManager.instance.laneKeys[noteinfo.location];
        if (!Input.GetKey(key))
        {
            // 끝나기 직전에 뗀 건 봐준다
            bool nearEnd = noteinfo.end_time - songTimeMs <= releaseWindow;
            Finish(nearEnd ? headGrade : "Miss");
        }
    }

    void Finish(string grade)
    {
        judged = true;
        ScoreManager.instance.RegisterJudge(grade);
        if (bodySr != null) bodySr.maskInteraction = SpriteMaskInteraction.None;
        Destroy(gameObject);
    }

    void UpdatePosition()
    {
        float remainMs = noteinfo.start_time - AudioManager.instance.TimeMs;
        Vector3 pos = transform.position;
        pos.y = global.instance.judgelineY + remainMs / 1000f * global.instance.notespeed;
        transform.position = pos;
    }
}
