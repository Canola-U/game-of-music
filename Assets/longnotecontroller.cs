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
    private float appliedSpeed = -1f;

    public void Init(note_data data)
    {
        noteinfo = data;

        if (body != null)
        {
            bodySr = body.GetComponent<SpriteRenderer>();
            baseHeight = bodySr.sprite.bounds.size.y;
            body.localPosition = Vector3.zero;
            bodySr.maskInteraction = SpriteMaskInteraction.None;
        }

        UpdateBodyLength();
        UpdatePosition();
    }

    // 몸통 길이 = 누르는 시간 × 노트 속도. 게임 중에 속도를 바꾸면 이미 내려오는 롱노트도 다시 맞춘다
    void UpdateBodyLength()
    {
        float speed = global.instance.notespeed;
        if (body == null || Mathf.Approximately(speed, appliedSpeed)) return;
        appliedSpeed = speed;

        float durationSec = (noteinfo.end_time - noteinfo.start_time) / 1000f;
        Vector3 scale = body.localScale;
        scale.y = durationSec * speed / baseHeight;
        body.localScale = scale;
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

        UpdateBodyLength();
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
