using UnityEngine;

public class StartScreenUI : MonoBehaviour
{
    void Update()
    {
        if (Input.anyKeyDown)
        {
            SceneFlow.instance.GoToSelect();
        }
    }
}