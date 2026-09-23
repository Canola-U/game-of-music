using UnityEngine;

public class global : MonoBehaviour
{
    public static global instance;

    public float notespeed = 8f;
    public float judgelineY = -2.5f;

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
}