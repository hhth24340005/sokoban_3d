using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class PauseViewButtonGroup : MonoBehaviour
{
  [SerializeField]
  private CanvasGroup group;

  [SerializeField]
  private Button resumeButton;

  [SerializeField]
  private Button returnToTitleButton;

  [SerializeField]
  private Vector3 positionOffset = new(-100f, 0f, 0f);

  [SerializeField]
  private float fadeInSeconds = 0.7f;

  [SerializeField]
  private float fadeOutSeconds = 0.4f;

  private Vector3? originalPosition = null;

  public async UniTask FadeInAsync(CancellationToken ct)
  {
    try
    {
      group.interactable = false;
      group.blocksRaycasts = false;

      originalPosition ??= transform.localPosition;
      transform.localPosition -= positionOffset;
      var moveTask =
        transform.DOLocalMove(
          endValue: (Vector3)originalPosition,
          duration: fadeInSeconds
        ).SetEase(Ease.OutQuad)
        .WithCancellation(ct);
      var fadeInTask =
        DOTween.To(
          getter: () => group.alpha,
          setter: x => group.alpha = x,
          endValue: 1f,
          duration: fadeInSeconds
        ).SetEase(Ease.InOutQuad)
        .WithCancellation(ct);
      await UniTask.WhenAll(moveTask, fadeInTask);
    }
    finally
    {
      group.interactable = true;
      group.blocksRaycasts = true;
    }
  }

  public async UniTask FadeOutAsync(CancellationToken ct)
  {
    group.interactable = false;
    group.blocksRaycasts = false;
    originalPosition ??= transform.localPosition;
    var moveTask =
      transform.DOLocalMove(
        endValue: (Vector3)originalPosition - positionOffset,
        duration: fadeOutSeconds
      ).SetEase(Ease.OutQuad)
      .WithCancellation(ct);
    var fadeOutTask =
      DOTween.To(
        getter: () => group.alpha,
        setter: x => group.alpha = x,
        endValue: 0f,
        duration: fadeOutSeconds
      ).SetEase(Ease.InOutQuad)
      .WithCancellation(ct);
    await UniTask.WhenAll(moveTask, fadeOutTask);
  }

  public async UniTask WaitForResumeButtonClickAsync(
    CancellationToken ct
  )
  {
    await resumeButton.OnClickAsync(ct);
  }

  public async UniTask WaitForReturnButtonClickAsync(
    CancellationToken ct
  ) => await returnToTitleButton.OnClickAsync(ct);
}
