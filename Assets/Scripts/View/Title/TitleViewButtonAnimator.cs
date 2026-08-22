using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TitleViewButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
  [SerializeField]
  private CanvasGroup parent;

  [SerializeField]
  private TMP_Text text;

  [SerializeField]
  private float scaleMultiplier = 1.1f;

  [SerializeField]
  private Color hoverColor = Color.yellow;

  [SerializeField]
  private float animationSeconds = 0.1f;

  private CancellationTokenSource _cts;

  private Vector3? _originalScale;

  private Color? _originalColor;

  public void OnPointerEnter(PointerEventData eventData)
  {
    if (parent?.interactable != true)
    {
      return;
    }

    _cts?.Cancel();
    var newCts = new CancellationTokenSource();
    _cts = newCts;
    _originalScale ??= text.transform.localScale;

    text.transform
      .DOScale(
        endValue: (Vector3)_originalScale * scaleMultiplier,
        duration: animationSeconds
      ).WithCancellation(newCts.Token)
      .Forget();

    _originalColor ??= text.color;
    DOTween.To(
      getter: () => text.color,
      setter: x => text.color = x,
      endValue: hoverColor,
      duration: animationSeconds
    ).WithCancellation(newCts.Token)
    .Forget();
  }

  public void OnPointerExit(PointerEventData eventData)
  {
    _cts?.Cancel();
    var newCts = new CancellationTokenSource();
    _cts = newCts;

    _originalScale ??= text.transform.localScale;
    text.transform
      .DOScale(
        endValue: (Vector3)_originalScale,
        duration: animationSeconds
      ).WithCancellation(newCts.Token)
      .Forget();

    _originalColor ??= text.color;
    DOTween.To(
      getter: () => text.color,
      setter: x => text.color = x,
      endValue: (Color)_originalColor,
      duration: animationSeconds
    ).WithCancellation(newCts.Token)
    .Forget();
  }
}
