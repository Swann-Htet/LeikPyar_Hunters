using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Smooth hover and press feel for a button: its colour and size ease, and an optional glow fades in.
// Works while the game is paused (it uses unscaled time).
public class UiHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Graphic target;
    public Color normal = Color.white;
    public Color hover = Color.white;
    public Color pressed = Color.white;

    public Graphic glow;
    public float glowNormal = 0f;
    public float glowHover = 0.5f;

    public float hoverScale = 1.02f;
    public float pressedScale = 0.97f;
    public float speed = 14f;

    private bool over;
    private bool down;
    private float scale = 1f;
    private Color color;
    private float glowAlpha;

    void OnEnable()
    {
        over = down = false;
        scale = 1f;
        color = normal;
        glowAlpha = glowNormal;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) { over = true; }
    public void OnPointerExit(PointerEventData eventData) { over = false; down = false; }
    public void OnPointerDown(PointerEventData eventData) { down = true; }
    public void OnPointerUp(PointerEventData eventData) { down = false; }

    void Update()
    {
        float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);

        Color goal = down ? pressed : over ? hover : normal;
        float goalScale = down ? pressedScale : over ? hoverScale : 1f;
        float goalGlow = over ? glowHover : glowNormal;

        color = Color.Lerp(color, goal, k);
        scale = Mathf.Lerp(scale, goalScale, k);
        glowAlpha = Mathf.Lerp(glowAlpha, goalGlow, k);
        Apply();
    }

    void Apply()
    {
        if (target != null) target.color = color;
        transform.localScale = new Vector3(scale, scale, 1f);

        if (glow != null)
        {
            Color c = glow.color;
            c.a = glowAlpha;
            glow.color = c;
        }
    }
}
