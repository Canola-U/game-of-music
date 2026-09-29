using UnityEngine;

public class GameConductor : MonoBehaviour
{
    public static bool gameStarted = false;

    [Tooltip("노래가 나오기 전 준비 시간(초). 이 동안 노트가 미리 내려온다")]
    public float leadInSeconds = 3f;

    private bool videoStarted = false;

    void Start()
    {
        gameStarted = false;
        AudioManager.instance.LoadSong(SongSelection.songFolder);
        VideoManager.instance.LoadVideo(SongSelection.songFolder);
    }

    void Update()
    {
        if (!gameStarted)
        {
            if (AudioManager.instance.clipReady && VideoManager.instance.videoReady)
            {
                AudioManager.instance.Play(leadInSeconds);
                gameStarted = true;
                Debug.Log($"[GameConductor] Started! lead-in {leadInSeconds}s");
            }
            return;
        }

        // 영상은 노래가 실제로 나오는 순간에 같이 튼다
        if (!videoStarted && AudioManager.instance.MusicStarted)
        {
            VideoManager.instance.Play();
            videoStarted = true;
        }
    }
}
