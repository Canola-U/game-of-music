using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFlow : MonoBehaviour
{
    public static SceneFlow instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartSong(string songFolder)
    {
        SongSelection.songFolder = songFolder;
        ScoreManager.instance.ResetScore();
        SceneManager.LoadScene("gamescreen");
    }

    public void RetrySong()
    {
        ScoreManager.instance.ResetScore();
        SceneManager.LoadScene("gamescreen");
    }

    public void GoToSelect()
    {
        ScoreManager.instance.ResetScore();
        SceneManager.LoadScene("selectscreen");
    }
}