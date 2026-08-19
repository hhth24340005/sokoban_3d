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
  private GameObject floor0Prefab;

  [SerializeField]
  private GameObject floor1Prefab;

  [SerializeField]
  private StageCamera stageCameraPrefab;

  public Vector3Int Size => size;

  public Vector3 Center => new(
    (size.x - 1) / 2f,
    (size.y - 1) / 2f,
    (size.z - 1) / 2f
  );

  public IReadOnlyDictionary<StageObject, IReadOnlyList<GameStage.Position>> GetGameObjectPositions()
  {
    var dict = new Dictionary<StageObject, List<GameStage.Position>>();
    foreach (var index in Enumerable.Range(0, cells.Length))
    {
      foreach (var obj in cells[index].StageObjects)
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

  public GameObject Floor0Prefab => floor0Prefab;

  public GameObject Floor1Prefab => floor1Prefab;

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
  private List<StageObject> stageObjects;

  public IReadOnlyList<StageObject> StageObjects => stageObjects;

  public Cell(IReadOnlyCollection<StageObject> stageObjects)
  {
    this.stageObjects = new(stageObjects);
  }
}
