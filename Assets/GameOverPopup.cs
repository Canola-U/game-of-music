using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class GameOverPopup : MonoBehaviour
{
    public GameObject popupPanel;
    public RectTransform buttonGroup;
    public Button retryButton;
    public Button selectButton;

    private Coroutine showRoutine;

    void Start()
    {
        popupPanel.SetActive(false);
    }

    public void Show()
    {
        popupPanel.SetActive(true);
        Time.timeScale = 0f;

        AudioManager.instance.Pause();
        VideoManager.instance.Pause();

        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(AnimateIn());
    }

    IEnumerator AnimateIn()
    {
        const float duration = 0.3f;

        Vector2 restPos = buttonGroup.anchoredPosition;
        Vector2 startPos = restPos + new Vector2(500f, 0f);
        buttonGroup.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out

            buttonGroup.anchoredPosition = Vector2.Lerp(startPos, restPos, eased);
            yield return null;
        }

        buttonGroup.anchoredPosition = restPos;

        if (EventSystem.current != null && retryButton != null)
        {
            EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
        }
    }

    public void OnRetryButton()
    {
        Time.timeScale = 1f;
        SceneFlow.instance.RetrySong();
    }

    public void OnSelectButton()
    {
        Time.timeScale = 1f;
        SceneFlow.instance.GoToSelect();
    }
}
