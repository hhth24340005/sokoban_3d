using UnityEngine;

[CreateAssetMenu(menuName = "Game/AssetRegistry")]
public sealed class AssetRegistry : ScriptableObject
{
  [SerializeField]
  private SystemRoot systemRoot;

  [SerializeField]
  private TitleView titleView;

  [SerializeField]
  private GameView gameView;

  public SystemRoot SystemRoot => systemRoot;

  public TitleView TitleView => titleView;

  public GameView GameView => gameView;
}
