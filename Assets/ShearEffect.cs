using UnityEngine;
using UnityEngine.UI;

// UI 오브젝트를 평행사변형처럼 비스듬하게 잘라 보이게 하는 메쉬 효과.
// (RectTransform 자체는 그대로라, 클릭 판정 영역은 사각형 그대로임)
[RequireComponent(typeof(Graphic))]
public class ShearEffect : BaseMeshEffect
{
    public float shearAmount = 40f; // 위쪽 모서리를 오른쪽으로 밀어낼 픽셀 양

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        RectTransform rect = transform as RectTransform;
        if (rect == null) return;

        float height = rect.rect.height;
        if (height <= 0f) return;

        UIVertex vertex = default;
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            float normalizedY = (vertex.position.y - rect.rect.yMin) / height; // 0(아래) ~ 1(위)
            vertex.position.x += normalizedY * shearAmount;
            vh.SetUIVertex(vertex, i);
        }
    }
}
