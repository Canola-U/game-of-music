using UnityEngine;

public class global : MonoBehaviour
{
    public static global instance;

    public float notespeed = 8f;
    public float judgelineY = -2.5f;

    // 노트 속도 (게임 중 +/- 키로 조절, 저장됨)
    public const float minNoteSpeed = 2f;
    public const float maxNoteSpeed = 20f;
    public const float noteSpeedStep = 0.1f;
    const string noteSpeedKey = "noteSpeed";

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            notespeed = Mathf.Clamp(PlayerPrefs.GetFloat(noteSpeedKey, notespeed), minNoteSpeed, maxNoteSpeed);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetNoteSpeed(float speed)
    {
        float snapped = Mathf.Round(speed / noteSpeedStep) * noteSpeedStep;
        notespeed = Mathf.Clamp(snapped, minNoteSpeed, maxNoteSpeed);
        PlayerPrefs.SetFloat(noteSpeedKey, notespeed);
        PlayerPrefs.Save();
    }
}
