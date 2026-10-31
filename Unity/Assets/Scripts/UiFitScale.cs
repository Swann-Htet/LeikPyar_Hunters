using UnityEngine;

// Scales a panel that was designed at a fixed size so it always fits the screen (1080p, 768p, phones...)
// without getting clipped or overflowing. The parent must be a full-screen rect.
public class UiFitScale : MonoBehaviour
{
    public Vector2 designSize = new Vector2(1640f, 940f);
    [Range(0.5f, 1f)] public float screenShare = 0.94f;
    public float maxScale = 1.2f;

    private float last = -1f;

    void LateUpdate()
    {
        var parent = transform.parent as RectTransform;
        if (parent == null) return;

        Vector2 size = parent.rect.size;
        float s = Mathf.Min(size.x * screenShare / designSize.x, size.y * screenShare / designSize.y, maxScale);
        if (Mathf.Abs(s - last) < 0.0005f) return;

        last = s;
        transform.localScale = new Vector3(s, s, 1f);
    }
}
