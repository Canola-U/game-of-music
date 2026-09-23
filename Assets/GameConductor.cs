using UnityEngine;

public class GameConductor : MonoBehaviour
{
    public static bool gameStarted = false;

    void Start()
    {
        gameStarted = false;
        AudioManager.instance.LoadSong(SongSelection.songFolder);
        VideoManager.instance.LoadVideo(SongSelection.songFolder);
    }

    void Update()
    {
        if (gameStarted) return;

        if (AudioManager.instance.clipReady && VideoManager.instance.videoReady)
        {
            AudioManager.instance.Play();
            VideoManager.instance.Play();
            gameStarted = true;
            Debug.Log("[GameConductor] Started! audio+video Play() called");
        }
    }
}