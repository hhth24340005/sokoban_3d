using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public sealed class StageCamera : MonoBehaviour
{
  [SerializeField]
  private Transform pivot;

  [SerializeField]
  private Camera camera;

  [SerializeField]
  private Transform compassNeedle;

  [SerializeField]
  private Transform compassNeedlePivot;

  [SerializeField]
  private float compassPitch = -20f;

  [SerializeField]
  private Ease compassEase = Ease.OutBounce;

  private Tween _compassTween;

  [SerializeField]
  private float animationSeconds = 1.5f;

  [SerializeField]
  private float distanceMultiplier = 1f;

  [SerializeField]
  private FloatRange pitchLimitDegrees = new(15f, 70f);

  public void Init(Bounds stage)
  {
    camera.transform.localPosition =
      new Vector3(
        0,
        0,
        -FitDistance(stage.extents.magnitude) * distanceMultiplier
      );
    pivot.transform.localPosition = stage.center;
    var angles = pivot.transform.localEulerAngles;
    angles.x = pitchLimitDegrees.Clamp(angles.x);
    pivot.transform.localEulerAngles = angles;
    RotateCompassNeedle().Complete();
  }

  public async UniTask OrbitAsync(
    CancellationToken ct,
    float deltaYaw = 0f,
    float deltaPitch = 0f
  )
  {
    var yaw = pivot.transform.localEulerAngles.y;
    var pitch = pivot.transform.localEulerAngles.x;
    if (180f < pitch)
    {
      pitch -= 360f;
    }
    var roll = pivot.transform.localEulerAngles.z;
    var newYaw = yaw + deltaYaw;
    var newPitch = pitchLimitDegrees.Clamp(pitch + deltaPitch);
    pivot.transform.localEulerAngles = new Vector3(newPitch, newYaw, roll);
    await RotateCompassNeedle().WithCancellation(ct);
  }

  public GameStage.Direction CameraLocalInput(GameStage.Direction world)
  {
    var ret = world;
    var rot = RotationOf(pivot.transform.localEulerAngles.y);
    for (var i = 0; i < rot; i++)
    {
      ret = Increment(ret);
    }
    return ret;

    static GameStage.Direction Increment(GameStage.Direction d) => d switch
    {
      GameStage.Direction.PlusZ => GameStage.Direction.PlusX,
      GameStage.Direction.PlusX => GameStage.Direction.MinusZ,
      GameStage.Direction.MinusZ => GameStage.Direction.MinusX,
      GameStage.Direction.MinusX => GameStage.Direction.PlusZ,
      _ => throw new System.Exception($"Unknown {nameof(GameStage.Direction)} type >.<"),
    };
  }

  private float FitDistance(float radius)
  {
    var verticalFov = camera.fieldOfView * Mathf.Deg2Rad;
    var horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov / 2f) * camera.aspect);
    var minFov = Mathf.Min(verticalFov, horizontalFov);
    return radius / Mathf.Sin(minFov / 2f);
  }

  private Tween RotateCompassNeedle()
  {
    _compassTween?.Kill();
    var angles = pivot.localEulerAngles;
    var needleAngles = new Vector3(compassPitch, -angles.y, 0f);
    var needlePivot = new Vector3(0f, 90f * RotationOf(angles.y), 0f);
    return _compassTween = DOTween.Sequence()
      .Append(
        compassNeedlePivot
          .DOLocalRotate(needlePivot, animationSeconds)
          .SetEase(compassEase)
      ).Join(
        compassNeedle
          .DOLocalRotate(needleAngles, animationSeconds)
          .SetEase(compassEase)
      );
  }

  private static int RotationOf(float yaw) => (int)((yaw + 45f) / 90);
}
