using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title flickers and now and then glitches sideways, like a bad tape
public class HorrorTitleFx : MonoBehaviour
{
    [Tooltip("A darker copy behind the title (optional)")]
    public TMP_Text glow;

    private TMP_Text text;
    private RectTransform rect;
    private Vector2 basePosition;
    private Color baseColor;
    private float seed;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        rect = (RectTransform)transform;
        basePosition = rect.anchoredPosition;
        baseColor = text != null ? text.color : Color.white;
        seed = Random.value * 100f;
    }

    void OnDisable()
    {
        if (rect != null) rect.anchoredPosition = basePosition;
    }

    void Update()
    {
        float t = Time.unscaledTime;
        bool glitch = Mathf.PerlinNoise(t * 2.2f, seed) > 0.9f;

        rect.anchoredPosition = basePosition + (glitch ? new Vector2(Random.Range(-10f, 10f), Random.Range(-3f, 3f)) : Vector2.zero);

        if (text != null)
        {
            float flicker = Mathf.PerlinNoise(t * 9f, seed) < 0.08f ? 0.55f : 1f;
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * flicker);
        }

        if (glow != null)
        {
            float pulse = 0.6f + 0.3f * Mathf.Sin(t * 1.3f + seed);
            var c = glow.color;
            c.a = pulse;
            glow.color = c;
        }
    }
}
