using UnityEngine;

[CreateAssetMenu(menuName = "Game/DefaultPreferences")]
public sealed class DefaultPreferences : ScriptableObject
{
  [SerializeField]
  private float cameraYawDegreesPerScreenHeight = 155f;

  [SerializeField]
  private float cameraPitchDegreesPerScreenHeight = -93f;

  public float CameraYawDegreesPerScreenHeight => cameraYawDegreesPerScreenHeight;

  public float CameraPitchDegreesPerScreenHeight => cameraPitchDegreesPerScreenHeight;
}
