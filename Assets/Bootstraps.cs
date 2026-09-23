using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstraps : MonoBehaviour
{
    void Start()
    {
        SceneManager.LoadScene("startscreen");
    }
}