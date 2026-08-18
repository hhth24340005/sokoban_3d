using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public sealed class StageCamera : MonoBehaviour
{
  [SerializeField]
  private Camera camera;

  [SerializeField]
  private Transform compassNeedle;

  [SerializeField]
  private Transform compassNeedlePivot;

  [SerializeField]
  private float animationSeconds = 1.5f;

  [SerializeField]
  private float distanceMultiplier = 1f;

  [SerializeField, Range(0, 1)]
  private float initialPitchFactor = 0.5f;

  [SerializeField]
  private FloatRange pitchLimitDegrees = new(15f, 70f);

  public void Init(Bounds stage)
  {
    camera.transform.localPosition =
      new(0, 0, -FitDistance(stage.extents.magnitude) * distanceMultiplier);
    var pitch = pitchLimitDegrees.Lerp(initialPitchFactor);
    transform.localPosition = stage.center;
    transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    compassNeedle.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
  }

  public async UniTask OrbitAsync(CancellationToken ct, float deltaYaw = 0f, float deltaPitch = 0f)
  {
    transform.DOKill();
    var yaw = transform.localEulerAngles.y;
    var pitch = transform.localEulerAngles.x;
    var roll = transform.localEulerAngles.z;
    var newYaw = yaw + deltaYaw;
    var newPitch = pitchLimitDegrees.Clamp(pitch + deltaPitch);

    var newRotation = Quaternion.Euler(newPitch, newYaw, roll);
    var needleQuart = Quaternion.Euler(-newPitch, -newYaw, -roll);
    var needlePivotQuart = Quaternion.Euler(0f, 90f * RotationOf(newYaw), 0f);

    transform.localRotation = newRotation;
    await new List<UniTask> {
      compassNeedle
        .DOLocalRotateQuaternion(needleQuart, animationSeconds)
        .SetEase(Ease.OutBounce)
        .WithCancellation(ct),
      compassNeedlePivot
        .DOLocalRotateQuaternion(needlePivotQuart, animationSeconds)
        .SetEase(Ease.OutQuad)
        .WithCancellation(ct),
    };
  }

  public GameStage.Direction CameraLocalInput(GameStage.Direction world)
  {
    static GameStage.Direction Increment(GameStage.Direction d) => d switch
    {
      GameStage.Direction.PlusZ => GameStage.Direction.PlusX,
      GameStage.Direction.PlusX => GameStage.Direction.MinusZ,
      GameStage.Direction.MinusZ => GameStage.Direction.MinusX,
      GameStage.Direction.MinusX => GameStage.Direction.PlusZ,
      _ => throw new System.Exception($"Unknown {nameof(GameStage.Direction)} type >.<"),
    };
    var ret = world;
    var rot = RotationOf(transform.localEulerAngles.y);
    for (var i = 0; i < rot; i++)
    {
      ret = Increment(ret);
    }
    return ret;
  }

  private float FitDistance(float radius)
  {
    var verticalFov = camera.fieldOfView * Mathf.Deg2Rad;
    var horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov / 2f) * camera.aspect);
    var minFov = Mathf.Min(verticalFov, horizontalFov);
    return radius / Mathf.Sin(minFov / 2f);
  }

  private int RotationOf(float yaw) => (int)((yaw + 45f) / 90);
}
