using UnityEngine;

[CreateAssetMenu(menuName = "Game/AssetRegistry")]
public sealed class AssetRegistry : ScriptableObject
{
  [SerializeField]
  private SystemRoot systemRoot;

  [SerializeField]
  private TitleView titleView;

  public SystemRoot SystemRoot => systemRoot;

  public TitleView TitleView => titleView;
}
