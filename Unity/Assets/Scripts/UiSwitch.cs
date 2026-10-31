using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A toggle switch: a pill with a round thumb that slides, and ON / OFF written on the free side.
public class UiSwitch : MonoBehaviour
{
    public Image track;
    public Image glow;
    public RectTransform thumb;
    public TMP_Text label;

    public Color onColor = new Color(0f, 0.9f, 0.83f, 1f);
    public Color offColor = new Color(0.33f, 0.38f, 0.45f, 0.55f);
    public Color onLabelColor = new Color(0.02f, 0.08f, 0.11f, 1f);
    public Color offLabelColor = new Color(0.65f, 0.71f, 0.79f, 1f);

    [Tooltip("How strong the red glow behind the switch is when it is ON")]
    public float glowAlpha = 0.35f;

    [Tooltip("How far the thumb and the label move from the middle")]
    public float travel = 56f;
    public float labelTravel = 28f;

    private bool on;
    private float t;
    private bool started;

    public void Set(bool value, bool instant)
    {
        on = value;
        if (instant || !started)
        {
            t = on ? 1f : 0f;
            started = true;
        }
        Apply();
    }

    void OnEnable()
    {
        t = on ? 1f : 0f;
        Apply();
    }

    void Update()
    {
        float goal = on ? 1f : 0f;
        if (Mathf.Approximately(t, goal)) return;

        t = Mathf.MoveTowards(t, goal, Time.unscaledDeltaTime / 0.16f);
        Apply();
    }

    void Apply()
    {
        float e = t * t * (3f - 2f * t);

        if (track != null) track.color = Color.Lerp(offColor, onColor, e);
        if (thumb != null) thumb.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, e), 0f);

        if (label != null)
        {
            label.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(labelTravel, -labelTravel, e), 0f);
            label.color = Color.Lerp(offLabelColor, onLabelColor, e);
        }

        if (glow != null)
        {
            Color c = glow.color;
            c.a = glowAlpha * e;
            glow.color = c;
        }
    }
}
