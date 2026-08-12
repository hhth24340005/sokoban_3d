using UnityEngine;

public sealed class StageCamera : MonoBehaviour
{
  [SerializeField]
  private Camera camera;

  [SerializeField]
  private float distanceMultiplier = 1f;

  [SerializeField, Range(0, 1)]
  private float initialPitchFactor = 0.5f;

  [SerializeField]
  private FloatRange pitchLimitDegrees = new(15f, 70f);

  public void Orbit(Bounds stage, float deltaYaw = 0f, float deltaPitch = 0f)
  {
    var yaw = transform.localEulerAngles.y;
    var pitch = transform.localEulerAngles.x;
    var roll = transform.localEulerAngles.z;
    var newYaw = yaw + deltaYaw;
    var newPitch = pitchLimitDegrees.Clamp(pitch + deltaPitch);
    var newRotation = Quaternion.Euler(newPitch, newYaw, roll);

    transform.SetLocalPositionAndRotation(stage.center, newRotation);
    camera.transform.localPosition =
      new(
        0,
        0,
        -FitDistance(stage.extents.magnitude) * distanceMultiplier
      );
  }

  private void Start()
  {
    transform.localRotation = Quaternion.Euler(pitchLimitDegrees.Lerp(initialPitchFactor), 0f, 0f);
  }

  private float FitDistance(float radius)
  {
    var verticalFov = camera.fieldOfView * Mathf.Deg2Rad;
    var horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov / 2f) * camera.aspect);
    var minFov = Mathf.Min(verticalFov, horizontalFov);
    return radius / Mathf.Sin(minFov / 2f);
  }
}
