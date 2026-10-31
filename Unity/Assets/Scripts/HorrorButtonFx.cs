using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Hover/press feel for horror buttons: grows, label glows blood red, and flickers when idle
public class HorrorButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text label;

    private Vector3 baseScale = Vector3.one;
    private Color labelColor = Color.white;
    private bool hover;
    private float seed;

    void Awake()
    {
        baseScale = transform.localScale;
        seed = Random.value * 100f;
        if (label != null) labelColor = label.color;
    }

    void OnDisable()
    {
        hover = false;
        transform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData) { hover = true; }
    public void OnPointerExit(PointerEventData eventData) { hover = false; }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        float s = hover ? 1.07f : 1f + Mathf.Sin(Time.unscaledTime * 1.6f + seed) * 0.012f;
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * s, dt * 12f);

        if (label == null) return;

        Color target = hover ? HorrorUI.BrightBlood : labelColor;
        if (!hover && Mathf.PerlinNoise(Time.unscaledTime * 3f, seed) < 0.12f) target.a *= 0.55f;
        label.color = Color.Lerp(label.color, target, dt * 14f);
    }
}
