using UnityEngine;

[CreateAssetMenu(menuName = "Game/AssetRegistry")]
public sealed class AssetRegistry : ScriptableObject
{
  [SerializeField]
  private TransitionView applicationEnterTransition;

  [SerializeField]
  private TitleView titleView;

  [SerializeField]
  private GameView gameView;

  [SerializeField]
  private DefaultPreferences defaultPreferences;

  // ===

  public TransitionView ApplicationEnterTransition => applicationEnterTransition;

  public TitleView TitleView => titleView;

  public GameView GameView => gameView;

  public DefaultPreferences DefaultPreferences => defaultPreferences;
}
