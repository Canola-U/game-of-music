using System.Collections.Generic;
using UnityEngine;

public class notegenerator : MonoBehaviour
{
    public GameObject note;
    public GameObject longnote;
    float notey = 10f;
    float[] notex = { -9.3f, -8.1f, -6.9f, -5.7f };

    private note_load loader;
    private List<note_data> notes;
    private int nextindex = 0;

    const float finishDelay = 5f;
    private bool allNotesSpawned = false;
    private float allNotesSpawnedTime = 0f;

    void Start()
    {
        loader = GetComponent<note_load>();
        notes = loader.notelist;
        Debug.Log("notegenerator Start! notes.Count=" + notes.Count + ", nextindex=" + nextindex);
    }

    void Update()
    {
        if (!GameConductor.gameStarted) return;

        if (nextindex >= notes.Count && !allNotesSpawned)
        {
            allNotesSpawned = true;
            allNotesSpawnedTime = Time.time;
        }

        if (allNotesSpawned && !ScoreManager.instance.isGameOver &&
            Time.time - allNotesSpawnedTime >= finishDelay)
        {
            ScoreManager.instance.SongComplete();
            return;
        }

        if (nextindex >= notes.Count) return;

        float SongTimeMs = AudioManager.instance.TimeMs;

        float travelDestance = notey - global.instance.judgelineY;
        float travelTimeMs = (travelDestance / global.instance.notespeed) * 1000f;

        while (nextindex < notes.Count &&
               SongTimeMs >= notes[nextindex].start_time - travelTimeMs)
        {
            SpawnNote(notes[nextindex], travelTimeMs);
            nextindex++;
        }
    }

    void SpawnNote(note_data note, float travelTimeMs)
    {
        float idealSpawnTimeMs = note.start_time - travelTimeMs;
        float lateMs = AudioManager.instance.TimeMs - idealSpawnTimeMs;
        float lateDistance = (lateMs / 1000f) * global.instance.notespeed;

        Vector3 SpawnPos = new Vector3(notex[note.location], notey - lateDistance, 0f);

        if (note.type == 1)
        {
            GameObject obj = Instantiate(longnote, SpawnPos, Quaternion.identity);
            longnoteController controller = obj.GetComponent<longnoteController>();
            if (controller != null)
            {
                controller.Init(note);
            }
        }
        else
        {
            GameObject obj = Instantiate(this.note, SpawnPos, Quaternion.identity);
            notecontroller controller = obj.GetComponent<notecontroller>();
            if (controller != null)
            {
                controller.Init(note);
            }
        }
    }
}