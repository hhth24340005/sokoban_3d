using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/AssetRegistry")]
public sealed class AssetRegistry : ScriptableObject
{
  [SerializeField]
  private TitleView titleView;

  [SerializeField]
  private GameView gameView;

  [SerializeField]
  private GameStagePreset[] gameStagePresets = { };

  public TitleView TitleView => titleView;

  public GameView GameView => gameView;

  public IReadOnlyList<GameStagePreset> GameStagePresets => gameStagePresets;
}
