using UnityEngine;

public sealed class SystemRoot : MonoBehaviour
{
  [SerializeField]
  private Camera mainCamera;

  [SerializeField]
  private Transform uiRoot;

  public Camera MainCamera => mainCamera;
  public Transform UIRoot => uiRoot;
}
