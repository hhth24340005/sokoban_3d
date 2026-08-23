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
  private float rewindDurationMultiplier = 0.5f;

  [SerializeField]
  private float walkSeconds = 0.2f;

  [SerializeField]
  protected AudioSource walkSound;

  [SerializeField]
  private Ease walkEase = Ease.OutQuad;

  [SerializeField]
  private Ease walkEaseRewind = Ease.InQuad;

  [SerializeField]
  private float gravityAcceleration = 9.8f;

  [SerializeField]
  private float climbApexOffset = 0.15f;

  [SerializeField]
  private float climbDurationMultiplier = 0.6f;

  private Tween _tween;

  public IReadOnlyCollection<GameStage.Rule> Rules => rules.Distinct().ToImmutableList();

  public async UniTask WalkAsync(
    ((float x, float z) past, (float x, float z) current) movement,
    bool rewind,
    CancellationToken ct
  )
  {
    if (walkSound)
    {
      walkSound.Play();
    }
    await AnimateAsync(
      () => Walk(movement, rewind),
      ct
    );
  }

  public async UniTask FallAsync(
    (float pastY, float currentY) movement,
    bool rewind,
    CancellationToken ct
  )
  {
    var (pastY, currentY) = (movement.pastY, movement.currentY);
    var target =
      new Vector3(
        transform.localPosition.x,
        rewind ? pastY : currentY,
        transform.localPosition.z
      );
    await AnimateAsync(
      () => (
        FreeFall(
          transform.localPosition.y,
          target.y,
          DurationMultiplierOf(rewind)
        ),
        target
      ),
      ct
    );
  }

  public async UniTask ClimbAsync(
    (Vector3 past, Vector3 current) movement,
    bool rewind,
    CancellationToken ct
  )
  {
    if (walkSound)
    {
      walkSound.Play();
    }
    var start = transform.localPosition;
    var target = rewind ? movement.past : movement.current;
    var apexY = Mathf.Max(start.y, target.y) + climbApexOffset;
    var multiplier = DurationMultiplierOf(rewind) * climbDurationMultiplier;
    var arc = MoveArc(apexY, target.y, multiplier);
    await AnimateAsync(
      () => (
        DOTween.Sequence()
          .Join(arc)
          .Join(LinearMoveXZ((target.x, target.z), arc.Duration())),
        target
      ),
      ct
    );
  }

  protected async UniTask AnimateAsync(
    Func<(Tween, Vector3 target)> animation,
    CancellationToken ct)
  {
    _tween?.Kill();
    var (tween, target) = animation();
    try
    {
      _tween = tween;
      await _tween.WithCancellation(ct);
    }
    finally
    {
      transform.localPosition = target;
    }
  }

  protected Tween LinearMoveXZ((float x, float z) target, float duration) =>
    DOTween.Sequence()
      .Join(
        transform.DOLocalMoveX(target.x, duration).SetEase(Ease.Linear)
      ).Join(
        transform.DOLocalMoveZ(target.z, duration).SetEase(Ease.Linear)
      );

  protected (Tween, Vector3) Walk(
    ((float x, float z) past, (float x, float z) current) movement,
    bool rewind
  )
  {
    var targetX = rewind ? movement.past.x : movement.current.x;
    var targetZ = rewind ? movement.past.z : movement.current.z;
    var tween =
      DOTween.Sequence()
        .Append(
          transform
            .DOLocalMoveX(targetX, walkSeconds * DurationMultiplierOf(rewind))
            .SetEase(rewind ? walkEaseRewind : walkEase)
        ).Join(
          transform
            .DOLocalMoveZ(targetZ, walkSeconds * DurationMultiplierOf(rewind))
            .SetEase(rewind ? walkEaseRewind : walkEase)
        );
    return (tween, new Vector3(targetX, transform.localPosition.y, targetZ));
  }

  private Tween FreeFall(
    float fromY,
    float toY,
    float durationMultiplier
  )
  {
    return
      transform
        .DOLocalMoveY(toY,
          FreeFallSeconds(fromY, toY) * durationMultiplier)
        .SetEase(fromY < toY ? Ease.OutQuad : Ease.InQuad);
  }

  protected Tween MoveArc(
    float apex,
    float target,
    float durationMultiplier
  ) => DOTween.Sequence()
    .Append(FreeFall(transform.localPosition.y, apex, durationMultiplier))
    .Append(FreeFall(apex, target, durationMultiplier));

  private float FreeFallSeconds(float y0, float y1) =>
    Mathf.Sqrt(2 * Mathf.Abs(y1 - y0) / gravityAcceleration);

  private float DurationMultiplierOf(bool rewind) =>
    rewind ? rewindDurationMultiplier : 1f;

}
