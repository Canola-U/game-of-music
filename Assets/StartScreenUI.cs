using UnityEngine;

public class StartScreenUI : MonoBehaviour
{
    void Update()
    {
        // ESC: 게임 종료, 그 외 아무 키: 곡 선택으로
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneFlow.instance.QuitGame();
        }
        else if (Input.anyKeyDown)
        {
            SceneFlow.instance.GoToSelect();
        }
    }
}
