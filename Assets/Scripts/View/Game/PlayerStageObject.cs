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
  private float bumpDistance = 0.2f;

  [SerializeField]
  private float bumpSeconds = 0.15f;

  [SerializeField]
  private AudioSource moveSound;

  private Tween _rotateTween;

  public async UniTask TurnAsync(GameStage.Direction direction, CancellationToken ct)
  {
    var lookRotation = Quaternion.LookRotation(VectorOf(direction));
    _rotateTween?.Kill();
    try
    {
      _rotateTween =
        transform.DOLocalRotateQuaternion(lookRotation, rotateAnimationSeconds);
      await _rotateTween.WithCancellation(ct);
    }
    finally
    {
      transform.localRotation = lookRotation;
    }
  }

  public UniTask BumpAsync(GameStage.Direction direction, CancellationToken ct)
  {
    var origin = transform.localPosition;
    var apex = origin + VectorOf(direction) * bumpDistance;
    return AnimateAsync(
      () => DOTween.Sequence()
        .Append(transform.DOLocalMove(apex, bumpSeconds * 0.5f).SetEase(Ease.OutQuad))
        .Append(transform.DOLocalMove(origin, bumpSeconds * 0.5f).SetEase(Ease.InQuad)),
      origin,
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
