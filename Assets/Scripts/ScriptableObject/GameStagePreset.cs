using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/GameStagePreset")]
public sealed class GameStagePreset : ScriptableObject
{
  [SerializeField]
  private StageSymbolTable symbolTable;

  [SerializeField]
  [TextArea]
  private List<string> cells = new();

  [SerializeField]
  private StageGround groundPrefab;

  [SerializeField]
  private StageCamera stageCameraPrefab;

  [SerializeField]
  private AudioClip music;

  private Vector3Int? _sizeCache;

  public Vector3Int Size =>
    _sizeCache ??= new Vector3Int(
      x: cells
        .Max(floor =>
          floor.Split(Environment.NewLine).Max(line => line.Length)
        ),
      y: cells.Count,
      z: cells
        .Max(floor =>
          floor.Split(Environment.NewLine).Length
        )
    );

  public Vector3 Center => new(
    (Size.x - 1) / 2f,
    (Size.y - 1) / 2f,
    (Size.z - 1) / 2f
  );

  public IReadOnlyDictionary<
    StageObject,
    IReadOnlyList<GameStage.Position>
  > GetGameObjectPositions()
  {
    var dict = new Dictionary<StageObject, List<GameStage.Position>>();
    foreach (var (floor, y) in cells.Select((it, y) => (it, Size.y - 1 - y)))
    {
      foreach (
        var (line, z) in
          floor
            .Split(Environment.NewLine)
            .Select((it, z) => (it, Size.z - 1 - z))
      )
      {
        foreach (var (cell, x) in line.Select((it, x) => (it, x)))
        {
          if (symbolTable.Table.TryGetValue(cell, out var obj))
          {
            dict.TryAdd(obj, new List<GameStage.Position>());
            dict[obj].Add(new GameStage.Position(x, y, z));
          }
          else if (cell != symbolTable.EmptySymbol)
          {
            Debug.LogError($"Unknown symbol: {cell}");
          }
        }
      }
    }
    return dict.ToImmutableDictionary(
      it => it.Key,
      it => (IReadOnlyList<GameStage.Position>)it.Value.ToImmutableList()
    );
  }

  public StageGround GroundPrefab => groundPrefab;

  public StageCamera StageCameraPrefab => stageCameraPrefab;

  public AudioClip Music => music;

  private void OnValidate()
  {
    _ = GetGameObjectPositions();
    _sizeCache = null;
    Debug.Log($"Current stage size: {Size}");
  }
}

