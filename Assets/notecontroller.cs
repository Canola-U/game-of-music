using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class notecontroller : MonoBehaviour
{
    public note_data noteinfo;

    public static List<notecontroller> activeNotes = new List<notecontroller>();

    public void Init(note_data data)
    {
        noteinfo = data;
    }

    void OnEnable()
    {
        activeNotes.Add(this);
    }

    void OnDisable()
    {
        activeNotes.Remove(this);
    }


    // Update is called once per frame
    void Update()
    {
        transform.position += Vector3.down * global.instance.notespeed * Time.deltaTime;

        if (transform.position.y < global.instance.judgelineY - 2f)
        {
            ScoreManager.instance.RegisterJudge("Miss");   // 추가
            Destroy(gameObject);
        }
    }
}
