using UnityEngine;

public readonly struct OrbitCamera
{
  public Vector3 Pivot { get; }
  public float YawDegrees { get; }
  public float PitchDegrees { get; }
  public float Distance { get; }
  public float MinPitchDegrees { get; }
  public float MaxPitchDegrees { get; }
  public float MinDistance { get; }
  public float MaxDistance { get; }

  public OrbitCamera(
    Vector3 pivot,
    float yawDegrees,
    float pitchDegrees,
    float distance,
    float minPitchDegrees,
    float maxPitchDegrees,
    float minDistance,
    float maxDistance
  )
  {
    Pivot = pivot;
    MinPitchDegrees = minPitchDegrees;
    MaxPitchDegrees = maxPitchDegrees;
    MinDistance = minDistance;
    MaxDistance = maxDistance;
    YawDegrees = NormalizeYaw(yawDegrees);
    PitchDegrees = Mathf.Clamp(pitchDegrees, minPitchDegrees, maxPitchDegrees);
    Distance = Mathf.Clamp(distance, minDistance, maxDistance);
  }

  public Quaternion Rotation => Quaternion.Euler(PitchDegrees, YawDegrees, 0f);

  public Vector3 Position => Pivot - Rotation * Vector3.forward * Distance;

  public OrbitCamera Rotated(float deltaYawDegrees, float deltaPitchDegrees) => new(
    Pivot,
    YawDegrees + deltaYawDegrees,
    PitchDegrees + deltaPitchDegrees,
    Distance,
    MinPitchDegrees,
    MaxPitchDegrees,
    MinDistance,
    MaxDistance
  );

  public OrbitCamera Zoomed(float deltaDistance) => new(
    Pivot,
    YawDegrees,
    PitchDegrees,
    Distance + deltaDistance,
    MinPitchDegrees,
    MaxPitchDegrees,
    MinDistance,
    MaxDistance
  );

  public void ApplyTo(Camera camera)
  {
    camera.transform.SetPositionAndRotation(Position, Rotation);
  }

  private static float NormalizeYaw(float yawDegrees)
  {
    var normalized = yawDegrees % 360f;
    return normalized < 0f ? normalized + 360f : normalized;
  }
}
