using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PauseViewButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

  private CancellationTokenSource cts = null;

  private Vector3? originalScale = null;

  private Color? originalColor = null;

  public void OnPointerEnter(PointerEventData eventData)
  {
    if (parent?.interactable != true)
    {
      return;
    }

    cts?.Cancel();
    var newCts = new CancellationTokenSource();
    cts = newCts;
    originalScale ??= text.transform.localScale;

    text.transform
      .DOScale(
        endValue: (Vector3)originalScale * scaleMultiplier,
        duration: animationSeconds
      ).WithCancellation(newCts.Token)
      .Forget();

    originalColor ??= text.color;
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
    cts?.Cancel();
    var newCts = new CancellationTokenSource();
    cts = newCts;

    originalScale ??= text.transform.localScale;
    text.transform
      .DOScale(
        endValue: (Vector3)originalScale,
        duration: animationSeconds
      ).WithCancellation(newCts.Token)
      .Forget();

    originalColor ??= text.color;
    DOTween.To(
      getter: () => text.color,
      setter: x => text.color = x,
      endValue: (Color)originalColor,
      duration: animationSeconds
    ).WithCancellation(newCts.Token)
    .Forget();
  }
}
