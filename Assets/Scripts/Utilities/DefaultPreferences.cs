using UnityEngine;

[CreateAssetMenu(menuName = "Game/DefaultPreferences")]
public sealed class DefaultPreferences : ScriptableObject
{
  [SerializeField]
  private float cameraYawDegreesPerPixel = 0.5f;

  [SerializeField]
  private float cameraPitchDegreesPerPixel = -0.3f;

  public float CameraYawDegreesPerPixel => cameraYawDegreesPerPixel;

  public float CameraPitchDegreesPerPixel => cameraPitchDegreesPerPixel;
}
