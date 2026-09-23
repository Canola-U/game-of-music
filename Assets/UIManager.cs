using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI comboText;
    public TextMeshProUGUI accuracyText;
    public Image hpFillImage;

    [Header("체력바를 트랙 옆에 자동으로 붙이기")]
    public RectTransform hpBarRect;
    public SpriteRenderer trackWallReference; // "wall r" 스프라이트 (이 벽의 실제 경계에 맞춤)
    public float hpBarGap = 14f;

    const int maxHp = 10;
    const float hpDrainSpeed = 0.8f; // 초당 이 비율만큼 목표치로 줄어듬 (1 = 1초에 꽉 찬 바 전체)

    private RectTransform comboRect;
    private Vector2 comboBasePos;
    private int lastCombo = 0;
    private Coroutine comboBounceRoutine;
    private float displayedHpRatio = 1f;

    void Start()
    {
        comboRect = comboText.rectTransform;
        comboBasePos = comboRect.anchoredPosition;
        comboText.text = "0";

        PositionHpBarNextToTrack();
    }

    // 트랙 벽(wall r)의 실제 경계(bounds)를 화면 좌표로 변환해서,
    // 해상도/화면비에 상관없이 체력바가 항상 벽 오른쪽 옆에, 벽 아래쪽과 높이를 맞춰 붙도록 함.
    // hpBarRect는 anchorMin=anchorMax=(0,0)이라, "화면 왼쪽 아래 모서리로부터 픽셀 거리" =
    // WorldToScreenPoint가 주는 좌표와 그대로 같음 (부모 Canvas의 pivot 값과 무관함)
    void PositionHpBarNextToTrack()
    {
        if (hpBarRect == null || Camera.main == null || trackWallReference == null) return;

        Bounds bounds = trackWallReference.bounds;
        Vector3 topScreen = Camera.main.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, 0f));
        Vector3 bottomScreen = Camera.main.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.min.y, 0f));

        float barX = topScreen.x + hpBarGap;
        float barBottomY = bottomScreen.y;

        hpBarRect.anchoredPosition = new Vector2(barX, barBottomY + hpBarRect.sizeDelta.y / 2f);
    }

    void Update()
    {
        if (ScoreManager.instance == null) return;

        int combo = ScoreManager.instance.combo;
        if (combo != lastCombo)
        {
            comboText.text = combo.ToString();

            if (combo > lastCombo && combo > 0)
            {
                if (comboBounceRoutine != null) StopCoroutine(comboBounceRoutine);
                comboBounceRoutine = StartCoroutine(BounceCombo());
            }

            lastCombo = combo;
        }

        accuracyText.text = ScoreManager.instance.AverageAccuracy.ToString("F1") + "%";

        if (hpFillImage != null)
        {
            float targetRatio = (float)ScoreManager.instance.hp / maxHp;
            displayedHpRatio = Mathf.MoveTowards(displayedHpRatio, targetRatio, Time.deltaTime * hpDrainSpeed);

            hpFillImage.fillAmount = displayedHpRatio;
            hpFillImage.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.85f, 0.35f), displayedHpRatio);
        }
    }

    IEnumerator BounceCombo()
    {
        const float duration = 0.18f;
        const float height = 18f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float bounce = Mathf.Sin(t * Mathf.PI) * height;

            comboRect.anchoredPosition = comboBasePos + new Vector2(0f, bounce);
            comboRect.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.25f);
            yield return null;
        }

        comboRect.anchoredPosition = comboBasePos;
        comboRect.localScale = Vector3.one;
    }
}
