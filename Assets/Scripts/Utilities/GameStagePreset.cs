using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/GameStagePreset")]
public sealed class GameStagePreset : ScriptableObject
{
  [SerializeField]
  private Vector3Int size = new(1, 1, 1);

  [SerializeField]
  private Cell[] cells = { };

  [SerializeField]
  private GameObject floorPrefab;

  [SerializeField]
  private GameObject wallPrefab;

  [SerializeField]
  private StageCamera stageCameraPrefab;

  public Vector3Int Size => size;

  public Vector3 Center => new(
    (size.x - 1) / 2f,
    (size.y - 1) / 2f,
    (size.z - 1) / 2f
  );

  public IReadOnlyDictionary<GameObject, IReadOnlyList<GameStage.Position>> GetGameObjectPositions()
  {
    var dict = new Dictionary<GameObject, List<GameStage.Position>>();
    foreach (var index in Enumerable.Range(0, cells.Length))
    {
      foreach (var obj in cells[index].GameObjects)
      {
        dict.TryAdd(obj, new());
        dict[obj].Add(ToPosition(index));
      }
    }
    return dict.ToImmutableDictionary(
      it => it.Key,
      it => (IReadOnlyList<GameStage.Position>)it.Value.ToImmutableList() // ??? Why am I supposed to cast
    );
  }

  public GameObject FloorPrefab => floorPrefab;

  public GameObject WallPrefab => wallPrefab;

  public StageCamera StageCameraPrefab => stageCameraPrefab;

  private GameStage.Position ToPosition(int index) =>
    new(
      X: index % size.x,
      Y: index / size.x % size.y,
      Z: index / size.x / size.y
    );
}

[Serializable]
public struct Cell
{
  [SerializeField]
  private List<GameObject> gameObjects;

  public IReadOnlyList<GameObject> GameObjects => gameObjects;

  public Cell(IReadOnlyCollection<GameObject> gameObjects)
  {
    this.gameObjects = new List<GameObject>(gameObjects);
  }
}
