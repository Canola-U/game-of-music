using UnityEngine;

public class GameConductor : MonoBehaviour
{
    public static bool gameStarted = false;

    [Tooltip("노래가 나오기 전 준비 시간(초). 이 동안 노트가 미리 내려온다")]
    public float leadInSeconds = 3f;

    private bool videoStarted = false;
    private bool leaving = false;

    void Start()
    {
        gameStarted = false;
        gameObject.AddComponent<PlayAdjustHud>();   // +/- 노트 속도, [/] 싱크 보정
        AudioManager.instance.LoadSong(SongSelection.songFolder);
        VideoManager.instance.LoadVideo(SongSelection.songFolder);
    }

    void Update()
    {
        // ESC: 곡 선택으로, F5: 처음부터 다시 (게임오버 팝업이 떠 있을 때도 동작)
        if (!leaving && !SceneFlow.instance.IsTransitioning)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Leave();
                SceneFlow.instance.GoToSelect();
                return;
            }
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Leave();
                SceneFlow.instance.RetrySong();
                return;
            }
        }

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

    // 노래를 멈추면 곡 시간도 멈춰서, 화면이 넘어가는 동안 노트가 Miss로 처리되지 않는다
    void Leave()
    {
        leaving = true;
        Time.timeScale = 1f;   // 게임오버 팝업이 멈춰둔 시간 되돌리기
        AudioManager.instance.Pause();
        VideoManager.instance.Pause();
    }
}
