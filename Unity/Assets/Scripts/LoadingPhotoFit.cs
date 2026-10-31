using UnityEngine;
using UnityEngine.UI;

// Keeps the loading photo in its own proportions on every screen:
//  - a screen that is close to the photo's shape (within 25%): the photo covers it (a little is cut at the edges)
//  - a screen that is much taller or wider (a phone held upright, a very wide window): the whole photo is shown
//    on black, so nothing important is cut and nothing is stretched
// It follows the screen when the phone is turned.
[RequireComponent(typeof(AspectRatioFitter))]
public class LoadingPhotoFit : MonoBehaviour
{
    public float photoAspect = 1.7778f;
    [Tooltip("How different the screen shape may be from the photo before the whole photo is shown (0.25 = 25%)")]
    public float coverTolerance = 0.25f;

    private AspectRatioFitter fitter;
    private RectTransform parent;

    void Awake()
    {
        fitter = GetComponent<AspectRatioFitter>();
        parent = transform.parent as RectTransform;
    }

    void Update()
    {
        if (parent == null || fitter == null) return;

        Rect r = parent.rect;
        if (r.height < 1f) return;

        float screenAspect = r.width / r.height;
        float ratio = screenAspect / Mathf.Max(0.01f, photoAspect);
        bool close = ratio > 1f / (1f + coverTolerance) && ratio < 1f + coverTolerance;

        fitter.aspectRatio = photoAspect;
        fitter.aspectMode = close ? AspectRatioFitter.AspectMode.EnvelopeParent : AspectRatioFitter.AspectMode.FitInParent;
    }
}
