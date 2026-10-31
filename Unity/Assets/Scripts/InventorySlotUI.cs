using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One square of the backpack. Built by Inventory (or by Tools > Ghost > Setup Inventory).
// Hover it to see the item's name, click it to hold the item in your hand.
public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image border;
    public Image fill;
    public Image icon;
    public TMP_Text letter;
    public TMP_Text nameText;
    public TMP_Text countText;

    public Color emptyColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);
    public Color fullColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    public Color borderNormal = new Color(0f, 0f, 0f, 0.6f);
    public Color borderEquipped = new Color(1f, 0.78f, 0.2f, 1f);
    public bool showBorder = true;

    // True for slots built by the setup tool (their colors come from the Inventory). False for your own design:
    // then the colors you made are kept as they are.
    [HideInInspector] public bool useStyleColors;

    [HideInInspector] public Inventory owner;
    [HideInInspector] public int index;

    private bool captured;
    private Color fillOriginal;
    private Color borderOriginal;
    private bool borderEnabledOriginal;

    // Remembers how the slot looked when you designed it
    void Capture()
    {
        if (captured) return;
        captured = true;

        if (fill != null) fillOriginal = fill.color;
        if (border != null)
        {
            borderOriginal = border.color;
            borderEnabledOriginal = border.enabled;
        }
    }

    public void Show(Inventory.Entry entry, bool showName, bool equipped)
    {
        Capture();

        bool has = entry != null;
        bool hasIcon = has && entry.icon != null;

        if (fill != null && useStyleColors) fill.color = has ? fullColor : emptyColor;

        if (icon != null)
        {
            icon.enabled = hasIcon;
            icon.sprite = hasIcon ? entry.icon : null;
        }

        // No picture: the first letter of the name instead
        if (letter != null)
        {
            letter.gameObject.SetActive(has && !hasIcon);
            if (has && !hasIcon && !string.IsNullOrEmpty(entry.displayName))
                letter.text = entry.displayName.Substring(0, 1).ToUpperInvariant();
        }

        if (nameText != null)
        {
            nameText.gameObject.SetActive(has && showName);
            if (has) nameText.text = entry.displayName;
        }

        // 2 or more of the same item: show how many
        if (countText != null)
        {
            countText.gameObject.SetActive(has && entry.count > 1);
            if (has) countText.text = "x" + entry.count;
        }

        // The frame (gold while the item is held in the hand)
        if (border != null)
        {
            if (useStyleColors)
            {
                border.enabled = showBorder || equipped;
                border.color = equipped ? borderEquipped : borderNormal;
            }
            else
            {
                border.enabled = borderEnabledOriginal || equipped;
                border.color = equipped ? borderEquipped : borderOriginal;
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null) owner.OnSlotHover(index, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null) owner.OnSlotHover(index, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null && eventData.button == PointerEventData.InputButton.Left)
            owner.OnSlotClicked(index);
    }
}
