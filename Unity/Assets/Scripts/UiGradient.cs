using UnityEngine;
using UnityEngine.UI;

// Colours a UI image with a gradient from the left edge to the right edge (cyan -> amber and so on).
public class UiGradient : BaseMeshEffect
{
    public Color left = Color.white;
    public Color right = Color.white;

    public void Set(Color newLeft, Color newRight)
    {
        left = newLeft;
        right = newRight;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        Rect rect = graphic.rectTransform.rect;
        float width = Mathf.Max(0.0001f, rect.width);

        var vertex = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            float t = Mathf.Clamp01((vertex.position.x - rect.xMin) / width);
            vertex.color = (Color)vertex.color * Color.Lerp(left, right, t);
            vh.SetUIVertex(vertex, i);
        }
    }
}
