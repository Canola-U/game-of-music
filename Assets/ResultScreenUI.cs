using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResultScreenUI : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI accuracyText;
    public TextMeshProUGUI maxComboText;
    public TMP_InputField nicknameInput;
    public Button saveButton;
    public TextMeshProUGUI saveButtonText;

    bool saved = false;

    void Start()
    {
        if (titleText != null) titleText.text = SongSelection.songFolder;
        accuracyText.text = ScoreManager.instance.AverageAccuracy.ToString("F1") + "%";
        maxComboText.text = "MAX COMBO " + ScoreManager.instance.maxCombo;
    }

    public void OnSaveButton()
    {
        if (saved) return;

        HighScoreManager.SaveRecord(SongSelection.songFolder, ScoreManager.instance.AverageAccuracy, nicknameInput.text);

        saved = true;
        if (saveButtonText != null) saveButtonText.text = "저장됨";
        if (saveButton != null) saveButton.interactable = false;
    }

    public void OnRetryButton()
    {
        SceneFlow.instance.RetrySong();
    }

    public void OnSelectButton()
    {
        SceneFlow.instance.GoToSelect();
    }
}
