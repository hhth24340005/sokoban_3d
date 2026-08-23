using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class PlayerStageObject : StageObject
{
  [SerializeField]
  private float rotateAnimationSeconds = 0.1f;

  [SerializeField]
  private AudioSource bumpSound;

  [SerializeField]
  private float bumpDistance = 0.2f;

  [SerializeField] private float bumpHeight = 0.4f;

  private Tween _rotateTween;

  public async UniTask TurnAsync(
    GameStage.Direction direction,
    CancellationToken ct
  )
  {
    var lookRotation = Quaternion.LookRotation(VectorOf(direction));
    _rotateTween?.Kill();
    try
    {
      _rotateTween =
        transform
          .DOLocalRotateQuaternion(lookRotation, rotateAnimationSeconds);
      await _rotateTween.WithCancellation(ct);
    }
    finally
    {
      transform.localRotation = lookRotation;
    }
  }

  public async UniTask BumpAsync(
    GameStage.Direction direction,
    CancellationToken ct
  )
  {
    var origin = transform.localPosition;
    var bumpAt = origin + VectorOf(direction) * bumpDistance;
    var (walk, _) =
      Walk(((origin.x, origin.z), (bumpAt.x, bumpAt.z)), false);
    var arc =
      MoveArc(bumpAt.y + bumpHeight, origin.y, 1f);
    var drawBack = LinearMoveXZ((origin.x, origin.z), arc.Duration());
    var animation =
      DOTween.Sequence()
        .Append(walk)
        .JoinCallback(() =>
        {
          if (walkSound)
          {
            walkSound.Play();
          }
        }).Append(arc)
        .Join(drawBack)
        .JoinCallback(() =>
        {
          if (bumpSound)
          {
            bumpSound.Play();
          }
        });
    await AnimateAsync(
      () => (animation, origin),
      ct
    );
  }

  private static Vector3 VectorOf(GameStage.Direction direction) =>
    direction switch
    {
      GameStage.Direction.PlusZ => Vector3.forward,
      GameStage.Direction.PlusX => Vector3.right,
      GameStage.Direction.MinusZ => Vector3.back,
      GameStage.Direction.MinusX => Vector3.left,
      _ => throw new Exception($"Unknown {nameof(GameStage.Direction)} type >.<"),
    };
}
