using UnityEngine;

[CreateAssetMenu(menuName = "Game/AssetRegistry")]
public sealed class AssetRegistry : ScriptableObject
{
  [SerializeField]
  private SystemRoot systemRoot;
  public SystemRoot SystemRoot => systemRoot;
}
