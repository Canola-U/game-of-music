using UnityEngine;
using UnityEngine.SceneManagement;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager instance;

    public int combo = 0;
    public int maxCombo = 0;

    public int perfectCount = 0;
    public int greatCount = 0;
    public int goodCount = 0;
    public int missCount = 0;

    private float totalPercent = 0f;
    private int noteCount = 0;

    public int hp = 10;
    const int maxHp = 10;

    public bool isGameOver = false;

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

    public float AverageAccuracy => noteCount == 0 ? 100f : totalPercent / noteCount;

    public void RegisterJudge(string grade)
    {
        if (isGameOver) return;

        float percent = GradeToPercent(grade);
        totalPercent += percent;
        noteCount++;

        switch (grade)
        {
            case "Perfect": perfectCount++; break;
            case "Great": greatCount++; break;
            case "Good": goodCount++; break;
            default: missCount++; break;
        }

        if (grade == "Miss")
        {
            combo = 0;
            hp--;

            if (hp <= 0)
            {
                GameOver();
            }
        }
        else
        {
            combo++;
            if (combo > maxCombo) maxCombo = combo;
        }
    }

    float GradeToPercent(string grade)
    {
        switch (grade)
        {
            case "Perfect": return 100f;
            case "Great": return 80f;
            case "Good": return 60f;
            default: return 0f;
        }
    }

    void GameOver()
    {
        isGameOver = true;
        FindObjectOfType<GameOverPopup>().Show();
    }

    public void SongComplete()
    {
        if (isGameOver) return;
        isGameOver = true;
        AudioManager.instance.Pause();
        VideoManager.instance.Pause();
        SceneManager.LoadScene("resultscreen");
    }

    public void ResetScore()
    {
        combo = 0;
        maxCombo = 0;
        perfectCount = 0;
        greatCount = 0;
        goodCount = 0;
        missCount = 0;
        totalPercent = 0f;
        noteCount = 0;
        hp = maxHp;
        isGameOver = false;
    }
}