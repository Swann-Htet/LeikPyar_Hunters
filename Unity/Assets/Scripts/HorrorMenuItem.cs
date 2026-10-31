using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One entry of the main menu: plain text that slides right, turns blood red and shows a diamond and a red line when hovered
public class HorrorMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public TMP_Text label;
    public Graphic marker;
    public Graphic line;
    [Tooltip("How far the text slides to the right (canvas units)")]
    public float slide = 18f;

    private RectTransform labelRect;
    private float baseLeft;
    private float amount;
    private bool hover;
    private float seed;

    void Awake()
    {
        seed = Random.value * 100f;
        if (label != null)
        {
            labelRect = (RectTransform)label.transform;
            baseLeft = labelRect.offsetMin.x;
        }
    }

    void OnEnable()
    {
        hover = false;
        amount = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) { hover = true; }
    public void OnPointerExit(PointerEventData eventData) { hover = false; }
    public void OnSelect(BaseEventData eventData) { hover = true; }
    public void OnDeselect(BaseEventData eventData) { hover = false; }

    void Update()
    {
        amount = Mathf.MoveTowards(amount, hover ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        Apply();
    }

    void Apply()
    {
        if (label != null)
        {
            Color c = Color.Lerp(HorrorUI.Bone, HorrorUI.BrightBlood, amount);
            if (amount < 0.01f && Mathf.PerlinNoise(Time.unscaledTime * 3f, seed) < 0.06f) c.a = 0.6f;
            label.color = c;

            var min = labelRect.offsetMin;
            min.x = baseLeft + slide * amount;
            labelRect.offsetMin = min;
        }

        if (marker != null)
        {
            var c = HorrorUI.BrightBlood;
            c.a = amount;
            marker.color = c;
        }

        if (line != null)
        {
            line.rectTransform.localScale = new Vector3(amount, 1f, 1f);
            var c = HorrorUI.Blood;
            c.a = 0.9f * amount;
            line.color = c;
        }
    }
}
