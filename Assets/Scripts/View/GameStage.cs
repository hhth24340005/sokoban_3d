using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public sealed class GameStage : IDisposable
{
  private readonly Vector3Int size;
  private readonly List<List<GameObject>> cells;
  private readonly ScopedGameObject root;

  private GameStage(Vector3Int size, List<List<GameObject>> cells, ScopedGameObject root)
  {
    this.size = size;
    this.cells = cells;
    this.root = root;
  }

  public static GameStage Create(GameStagePreset preset, GameObject parent)
  {
    var root = parent.ChildOf(nameof(GameStage));
    var cells = new List<List<GameObject>>(preset.Cells.Count);
    for (var index = 0; index < preset.Cells.Count; index++)
    {
      var (x, y, z) = FromIndex(index, preset.Size);
      var cell = new List<GameObject>();
      foreach (var prefab in preset.Cells[index].GameObjects)
      {
        var spawned = root.GameObject.ChildOf(prefab).GameObject;
        spawned.GetComponent<IStageObjectView>()?.TeleportTo(x, y, z);
        cell.Add(spawned);
      }
      cells.Add(cell);
    }
    return new GameStage(preset.Size, cells, root);
  }

  public void Dispose() => root.Dispose();

  public bool InBounds(int x, int y, int z) =>
    0 <= x && x < size.x &&
    0 <= y && y < size.y &&
    0 <= z && z < size.z;

  public IReadOnlyList<T> Get<T>(int x, int y, int z) where T : IStageObject
  {
    if (!InBounds(x, y, z))
    {
      throw new ArgumentOutOfRangeException();
    }
    return CellToModels(cells[ToIndex(x, y, z)], new Vector3Int(x, y, z))
      .OfType<T>()
      .ToImmutableList();
  }

  public IReadOnlyList<T> GetAll<T>() where T : IStageObject
  {
    var result = new List<T>();
    for (var index = 0; index < cells.Count; index++)
    {
      var (x, y, z) = FromIndex(index, size);
      result.AddRange(
        CellToModels(cells[index], new Vector3Int(x, y, z)).OfType<T>()
      );
    }
    return result.ToImmutableList();
  }

  private IEnumerable<IStageObject> CellToModels(List<GameObject> cell, Vector3Int position) =>
    cell
      .Select(it => it.GetComponent<IStageObjectView>())
      .NotNull()
      .Select(view => ToModel(view, position));

  private int ToIndex(int x, int y, int z) =>
    x + y * size.x + z * size.x * size.y;

  private static (int x, int y, int z) FromIndex(int index, Vector3Int size) =>
    (index % size.x, index / size.x % size.y, index / (size.x * size.y));

  private void Relocate(IMovableView view, Vector3Int target)
  {
    var gameObject = ((Component)view).gameObject;
    foreach (var cell in cells)
    {
      if (cell.Remove(gameObject))
      {
        break;
      }
    }
    cells[ToIndex(target.x, target.y, target.z)].Add(gameObject);
  }

  internal IStageObject ToModel(IStageObjectView view, Vector3Int position) => view switch
  {
    SubjectBoxView box => new SubjectBoxStageObjectAdapter(this, box, position),
    PlayerView player => new ControllableStageObjectAdapter(this, player, position),
    GoalView => new GoalStageObjectAdapter(position),
    _ => throw new NotSupportedException(
      $"GameStage does not know how to translate {view.GetType()} into a {nameof(IStageObject)}."
    ),
  };

  private class MovableStageObjectAdapter : IMovableStageObject
  {
    private readonly GameStage stage;
    private readonly IMovableView view;

    public Vector3Int Position { get; }

    public MovableStageObjectAdapter(GameStage stage, IMovableView view, Vector3Int position)
    {
      this.stage = stage;
      this.view = view;
      Position = position;
    }

    public async UniTask MoveTo(
      int x,
      int y,
      int z,
      MoveMode moveMode,
      CancellationToken ct
    )
    {
      stage.Relocate(view, new Vector3Int(x, y, z));
      await view.MoveTo(x, y, z, moveMode, ct);
    }
  }

  private sealed class ControllableStageObjectAdapter :
    MovableStageObjectAdapter,
    IControllableStageObject
  {
    public ControllableStageObjectAdapter(GameStage stage, IMovableView view, Vector3Int position)
      : base(stage, view, position) { }
  }

  private sealed class SubjectBoxStageObjectAdapter :
    MovableStageObjectAdapter,
    ISubjectBoxStageObject
  {
    public SubjectBoxStageObjectAdapter(GameStage stage, IMovableView view, Vector3Int position)
      : base(stage, view, position) { }
  }

  private sealed class GoalStageObjectAdapter : IGoalStageObject
  {
    public Vector3Int Position { get; }

    public GoalStageObjectAdapter(Vector3Int position)
    {
      Position = position;
    }
  }
}
