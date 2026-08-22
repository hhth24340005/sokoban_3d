using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public sealed class FadingTransitionView : TransitionView
{
  [SerializeField]
  private CanvasGroup overlay;

  [SerializeField]
  private float fadeInSeconds = 2f;

  [SerializeField]
  private float fadeOutSeconds = 2f;

  [SerializeField]
  private Ease fadeInEase = Ease.InOutSine;

  [SerializeField]
  private Ease fadeOutEase = Ease.InOutSine;

  public override Func<CancellationToken, UniTask> CoverInstant()
  {
    overlay.DOKill();
    overlay.gameObject.SetActive(true);
    overlay.blocksRaycasts = true;
    overlay.alpha = 1f;

    return RevealAsync;
  }

  public override async UniTask<Func<CancellationToken, UniTask>> CoverAsync(CancellationToken ct)
  {
    try
    {
      overlay.DOKill();
      overlay.alpha = 0f;
      overlay.gameObject.SetActive(true);
      overlay.blocksRaycasts = true;

      await DOTween.To(
        getter: () => overlay.alpha,
        setter: x => overlay.alpha = x,
        endValue: 1f,
        duration: fadeOutSeconds
      ).SetEase(fadeOutEase)
      .WithCancellation(ct);

      return RevealAsync;
    }
    finally
    {
      overlay.alpha = 1f;
    }
  }

  private async UniTask RevealAsync(CancellationToken ct)
  {
    if (gameObject == null)
    {
      return;
    }
    overlay.DOKill();
    overlay.alpha = 1f;
    overlay.blocksRaycasts = true;
    overlay.gameObject.SetActive(true);
    await DOTween.To(
      getter: () => overlay.alpha,
      setter: x => overlay.alpha = x,
      endValue: 0f,
      duration: fadeInSeconds
    ).SetEase(fadeInEase)
    .WithCancellation(ct);
    Destroy(gameObject);
  }
}
