using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The vignette breathes a little, like a failing light
public class HorrorVignetteFx : MonoBehaviour
{
    public float strength = 0.75f;
    private Image image;
    private float seed;

    void Awake()
    {
        image = GetComponent<Image>();
        seed = Random.value * 100f;
    }

    void Update()
    {
        if (image == null) return;

        float slow = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 0.8f + seed);
        float flick = Mathf.PerlinNoise(Time.unscaledTime * 6f, seed) < 0.1f ? 0.75f : 1f;
        image.color = new Color(1f, 1f, 1f, strength * slow * flick);
    }
}
