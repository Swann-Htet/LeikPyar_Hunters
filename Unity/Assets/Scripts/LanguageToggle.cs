using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LanguageToggle : MonoBehaviour
{
    public TMP_Text label;

    private TMP_FontAsset englishFont;
    private Material englishMaterial;

    void Awake()
    {
        if (label == null) return;

        englishFont = label.font;
        englishMaterial = label.fontSharedMaterial;
        if (label.GetComponent<LocalizeIgnore>() == null) label.gameObject.AddComponent<LocalizeIgnore>();
    }

    void OnEnable()
    {
        GetComponent<Button>().onClick.AddListener(Toggle);
        Loc.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        GetComponent<Button>().onClick.RemoveListener(Toggle);
        Loc.Changed -= Refresh;
    }

    void Toggle()
    {
        Loc.Set(Loc.IsBurmese ? Loc.Language.English : Loc.Language.Burmese);
    }

    void Refresh()
    {
        if (label == null) return;

        if (Loc.IsBurmese)
        {
            if (englishFont != null) label.font = englishFont;
            if (englishMaterial != null) label.fontSharedMaterial = englishMaterial;
            label.text = "ENGLISH";
            return;
        }

        string name = Loc.Raw("@language_name");
        if (Loc.BurmeseFont != null && name.Length > 0)
        {
            label.font = Loc.BurmeseFont;
            label.text = name;
        }
        else
        {
            label.text = "MYANMAR";
        }
    }
}
