
using UnityEngine;

public class longnoteController : MonoBehaviour
{
    public note_data noteinfo;
    public Transform body;

    private bool started = false;
    const float startWindow = 150f;

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
    }

    void Update()
    {
        transform.position += Vector3.down * global.instance.notespeed * Time.deltaTime;

        float songTimeMs = AudioManager.instance.TimeMs;
        KeyCode key = KeyManager.instance.laneKeys[noteinfo.location];

        if (!started)
        {
            if (Input.GetKey(key))
            {
                float diff = Mathf.Abs(songTimeMs - noteinfo.start_time);
                if (diff <= startWindow)
                {
                    started = true;
                    bodySr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
                }
            }

            if (transform.position.y < global.instance.judgelineY - 2f)
            {
                ScoreManager.instance.RegisterJudge("Miss");   // 추가: 시작조차 못 함
                Destroy(gameObject);
            }
        }
        else
        {
            if (!Input.GetKey(key))
            {
                ScoreManager.instance.RegisterJudge("Miss");   // 추가: 중간에 뗌
                bodySr.maskInteraction = SpriteMaskInteraction.None;
                Destroy(gameObject);
                return;
            }

            if (songTimeMs >= noteinfo.end_time)
            {
                ScoreManager.instance.RegisterJudge("Perfect");   // 추가: 완주 성공 (일단 Perfect로 고정)
                Destroy(gameObject);
            }
        }
    }
}