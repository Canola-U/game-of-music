using UnityEngine;

public class KeyManager : MonoBehaviour
{
    public static KeyManager instance;

    public KeyCode[] laneKeys = { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadKeys();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 특정 레인의 키를 새 키로 바꾸기
    public void RebindKey(int laneIndex, KeyCode newKey)
    {
        laneKeys[laneIndex] = newKey;
        PlayerPrefs.SetString("laneKey" + laneIndex, newKey.ToString());
        PlayerPrefs.Save();
    }

    // 저장된 키 불러오기 (없으면 기본값 유지)
    void LoadKeys()
    {
        for (int i = 0; i < laneKeys.Length; i++)
        {
            if (PlayerPrefs.HasKey("laneKey" + i))
            {
                string saved = PlayerPrefs.GetString("laneKey" + i);
                laneKeys[i] = (KeyCode)System.Enum.Parse(typeof(KeyCode), saved);
            }
        }
    }
}
