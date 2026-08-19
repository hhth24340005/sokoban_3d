using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class PlayerStageObject : StageObject
{
  [SerializeField]
  private float rotateAnimationSeconds = 0.1f;

  public override async UniTask MoveTo(Vector3 target, CancellationToken ct)
  {
    var moveTask = base.MoveTo(target, ct);
    var rotateTask = UniTask.CompletedTask;
    if (transform.localPosition.x != target.x || transform.localPosition.z != target.z)
    {
      var lookAt = target;
      lookAt.y = transform.localPosition.y;
      var lookRotation = Quaternion.LookRotation(lookAt - transform.localPosition);
      rotateTask =
        transform
          .DOLocalRotateQuaternion(lookRotation, rotateAnimationSeconds)
          .WithCancellation(ct);
      ct.Register(() => transform.localRotation = lookRotation);
    }
    await UniTask.WhenAll(moveTask, rotateTask);
  }

  public override async UniTask RewindTo(Vector3 target, CancellationToken ct)
  {
    var moveTask = base.RewindTo(target, ct);
    var rotateTask = UniTask.CompletedTask;
    if (transform.localPosition.x != target.x || transform.localPosition.z != target.z)
    {
      var lookAt = target;
      lookAt.y = transform.localPosition.y;
      var lookRotation = Quaternion.LookRotation(transform.localPosition - lookAt);
      rotateTask =
        transform
          .DOLocalRotateQuaternion(lookRotation, rotateAnimationSeconds)
          .WithCancellation(ct);
      ct.Register(() => transform.localRotation = lookRotation);
    }
    await UniTask.WhenAll(moveTask, rotateTask);
  }
}
