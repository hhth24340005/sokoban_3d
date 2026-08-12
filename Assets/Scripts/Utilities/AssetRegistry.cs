using UnityEngine;

[CreateAssetMenu(menuName = "Game/AssetRegistry")]
public sealed class AssetRegistry : ScriptableObject
{
  [SerializeField]
  private TitleView titleView;

  [SerializeField]
  private GameView gameView;

  [SerializeField]
  private DefaultPreferences defaultPreferences;

  public TitleView TitleView => titleView;

  public GameView GameView => gameView;

  public DefaultPreferences DefaultPreferences => defaultPreferences;
}
