using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class StageObject : MonoBehaviour
{
  [SerializeField]
  private List<GameStage.Rule> rules = new();

  [SerializeField]
  private float animationSeconds = 0.15f;

  [SerializeField]
  private float rewindDurationMultiplier = 0.25f;

  [SerializeField]
  private float gravityAcceleration = 9.8f;

  [SerializeField]
  private Ease ease = Ease.OutQuad;

  [SerializeField]
  private Ease easeRewind = Ease.InQuad;

  public IReadOnlyCollection<GameStage.Rule> Rules => rules.Distinct().ToImmutableList();

  public async UniTask MoveTo(Vector3 target, CancellationToken ct)
  {
    transform.DOKill();
    try
    {
      var x = transform.DOLocalMoveX(
        endValue: target.x,
        duration: animationSeconds
      ).SetEase(ease)
      .WithCancellation(ct);

      UniTask y;
      if (target.y < transform.localPosition.y)
      {
        var fallDistance = transform.localPosition.y - target.y;
        var fallDuration = Mathf.Sqrt(2 * fallDistance / gravityAcceleration);
        y = transform.DOLocalMoveY(
          endValue: target.y,
          duration: fallDuration
        ).SetEase(Ease.InQuad)
        .WithCancellation(ct);
      }
      else
      {
        y = transform.DOLocalMoveY(
          endValue: target.y,
          duration: animationSeconds
        ).SetEase(ease)
        .WithCancellation(ct);
      }

      var z = transform.DOLocalMoveZ(
        endValue: target.z,
        duration: animationSeconds
      ).SetEase(ease)
      .WithCancellation(ct);

      await UniTask.WhenAll(x, y, z);
    }
    finally
    {
      transform.localPosition = target;
    }
  }

  public async UniTask RewindTo(Vector3 target, CancellationToken ct)
  {
    transform.DOKill();
    try
    {
      var x = transform.DOLocalMoveX(
        endValue: target.x,
        duration: animationSeconds * rewindDurationMultiplier
      ).SetEase(easeRewind)
      .WithCancellation(ct);

      UniTask y;
      if (transform.localPosition.y < target.y)
      {
        var riseDistance = target.y - transform.localPosition.y;
        var riseDuration = Mathf.Sqrt(2 * riseDistance / gravityAcceleration);
        y = transform.DOLocalMoveY(
          endValue: target.y,
          duration: riseDuration * rewindDurationMultiplier
        ).SetEase(Ease.OutQuad)
        .WithCancellation(ct);
      }
      else
      {
        y = transform.DOLocalMoveY(
          endValue: target.y,
          duration: animationSeconds * rewindDurationMultiplier
        ).SetEase(ease)
        .WithCancellation(ct);
      }

      var z = transform.DOLocalMoveZ(
        endValue: target.z,
        duration: animationSeconds * rewindDurationMultiplier
      ).SetEase(easeRewind)
      .WithCancellation(ct);

      await UniTask.WhenAll(x, y, z);
    }
    finally
    {
      transform.localPosition = target;
    }
  }
}
