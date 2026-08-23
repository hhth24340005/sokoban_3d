using System;
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
  private float animationSeconds = 0.2f;

  [SerializeField]
  private float rewindDurationMultiplier = 0.5f;

  [SerializeField]
  private float gravityAcceleration = 9.8f;

  [SerializeField]
  private float climbApexOffset = 0.15f;

  [SerializeField]
  private float climbDurationMultiplier = 0.6f;

  [SerializeField]
  private Ease ease = Ease.OutQuad;

  [SerializeField]
  private Ease easeRewind = Ease.InQuad;

  private Tween _moveTween;

  public IReadOnlyCollection<GameStage.Rule> Rules => rules.Distinct().ToImmutableList();

  public async UniTask WalkAsync(
    GameStage.Movement movement,
    bool rewind,
    CancellationToken ct
  )
  {
    var target = TargetOf(movement, rewind);
    await AnimateAsync(
      () => transform
        .DOLocalMove(target, DurationOf(animationSeconds, rewind))
        .SetEase(rewind ? easeRewind : ease),
      target,
      ct
    );
  }

  public async UniTask FallAsync(
    GameStage.Movement movement,
    bool rewind,
    CancellationToken ct
  )
  {
    var start = transform.localPosition;
    var target = TargetOf(movement, rewind);
    await AnimateAsync(
      () => ArcY(start.y, target.y, DurationMultiplier(rewind)),
      target,
      ct
    );
  }

  public async UniTask ClimbAsync(
    GameStage.Movement movement,
    bool rewind,
    CancellationToken ct
  )
  {
    if (walkSound)
    {
      walkSound.Play();
    }
    var start = transform.localPosition;
    var target = TargetOf(movement, rewind);
    var apexY = Mathf.Max(start.y, target.y) + climbApexOffset;
    var multiplier = DurationMultiplier(rewind) * climbDurationMultiplier;
    var total =
      (ArcSeconds(start.y, apexY) + ArcSeconds(apexY, target.y)) * multiplier;
    await AnimateAsync(
      () => DOTween.Sequence()
        .Append(ArcY(start.y, apexY, multiplier))
        .Append(ArcY(apexY, target.y, multiplier))
        .Insert(0, transform.DOLocalMoveX(target.x, total).SetEase(Ease.Linear))
        .Insert(0, transform.DOLocalMoveZ(target.z, total).SetEase(Ease.Linear)),
      target,
      ct
    );
  }

  private Tween ArcY(float fromY, float toY, float multiplier) =>
    transform
      .DOLocalMoveY(toY, ArcSeconds(fromY, toY) * multiplier)
      .SetEase(fromY < toY ? Ease.OutQuad : Ease.InQuad);

  private float ArcSeconds(float fromY, float toY) =>
    Mathf.Sqrt(2 * Mathf.Abs(toY - fromY) / gravityAcceleration);

  protected float DurationOf(float seconds, bool rewind) =>
    seconds * DurationMultiplier(rewind);

  protected float DurationMultiplier(bool rewind) =>
    rewind ? rewindDurationMultiplier : 1f;

  protected static Vector3 TargetOf(GameStage.Movement movement, bool rewind)
  {
    var (x, y, z) = rewind ? movement.From : movement.To;
    return new Vector3(x, y, z);
  }

  protected async UniTask AnimateAsync(Func<Tween> tween, Vector3 target, CancellationToken ct)
  {
    _moveTween?.Kill();
    try
    {
      _moveTween = tween();
      await _moveTween.WithCancellation(ct);
    }
    finally
    {
      transform.localPosition = target;
    }
  }
}
