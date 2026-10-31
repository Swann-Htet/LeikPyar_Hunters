using System;
using UnityEngine;
using UnityEngine.EventSystems;

// A phone button that reacts the moment the finger touches it (not when it is let go), and shrinks a little while pressed.
public class MobilePressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action onPress;
    public float pressedScale = 0.9f;

    private Vector3 normalScale = Vector3.one;
    private bool pressed;

    void Awake()
    {
        normalScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        transform.localScale = normalScale * pressedScale;
        if (onPress != null) onPress();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Release();
    }

    void Release()
    {
        if (!pressed) return;
        pressed = false;
        transform.localScale = normalScale;
    }

    void OnDisable()
    {
        Release();
    }
}
