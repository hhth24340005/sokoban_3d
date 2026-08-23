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
  private float climbJumpPower = 0.6f;

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
        .DOLocalMove(target, DurationOf(rewind))
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
    var target = TargetOf(movement, rewind);
    var distance = Mathf.Abs(target.y - transform.localPosition.y);
    var duration =
      Mathf.Sqrt(2 * distance / gravityAcceleration) *
        DurationMultiplier(rewind);
    await AnimateAsync(
      () => transform
        .DOLocalMove(target, duration)
        .SetEase(rewind ? Ease.OutQuad : Ease.InQuad),
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
    var target = TargetOf(movement, rewind);
    await AnimateAsync(
      () => transform.DOLocalJump(
        endValue: target,
        jumpPower: climbJumpPower,
        numJumps: 1,
        duration: DurationOf(rewind)
      ),
      target,
      ct
    );
  }

  protected float DurationOf(bool rewind) =>
    animationSeconds * DurationMultiplier(rewind);

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
