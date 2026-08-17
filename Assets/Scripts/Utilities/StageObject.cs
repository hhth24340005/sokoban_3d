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
  private Ease ease = Ease.OutQuad;

  public IReadOnlyCollection<GameStage.Rule> Rules => rules.Distinct().ToImmutableList();

  public async UniTask MoveTo(Vector3 target, CancellationToken ct)
  {
    try
    {
      await transform.DOLocalMove(
        endValue: target,
        duration: animationSeconds
      ).SetEase(ease)
      .WithCancellation(ct);
    }
    finally
    {
      transform.localPosition = target;
    }
  }
}
