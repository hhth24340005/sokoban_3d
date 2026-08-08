using UnityEngine;

[CreateAssetMenu(menuName = "Game/CameraSettings")]
public sealed class CameraSettings : ScriptableObject
{
  [SerializeField]
  private float initialYawDegrees = 0f;

  [SerializeField]
  private float initialPitchDegrees = 45f;

  [SerializeField]
  private float initialDistance = 8f;

  [SerializeField]
  private float yawDegreesPerPixel = 0.25f;

  [SerializeField]
  private float pitchDegreesPerPixel = 0.15f;

  [SerializeField]
  private float distancePerScrollNotch = 1.5f;

  [SerializeField]
  private float headlightIntensity = 0.6f;

  [SerializeField]
  private float minPitchDegrees = 25f;

  [SerializeField]
  private float maxPitchDegrees = 70f;

  [SerializeField]
  private float minDistance = 3f;

  [SerializeField]
  private float maxDistance = 15f;

  public float InitialYawDegrees => initialYawDegrees;

  public float InitialPitchDegrees => initialPitchDegrees;

  public float InitialDistance => initialDistance;

  public float YawDegreesPerPixel => yawDegreesPerPixel;

  public float PitchDegreesPerPixel => pitchDegreesPerPixel;

  public float DistancePerScrollNotch => distancePerScrollNotch;

  public float HeadlightIntensity => headlightIntensity;

  public float MinPitchDegrees => minPitchDegrees;

  public float MaxPitchDegrees => maxPitchDegrees;

  public float MinDistance => minDistance;

  public float MaxDistance => maxDistance;
}
