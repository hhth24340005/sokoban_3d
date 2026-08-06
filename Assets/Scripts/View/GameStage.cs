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

  public IReadOnlyList<T> Get<T>(int x, int y, int z) where T : IStageObject
  {
    if (
      x < 0 || size.x <= x ||
      y < 0 || size.y <= y ||
      z < 0 || size.z <= z
    )
    {
      throw new ArgumentOutOfRangeException();
    }
    return cells[ToIndex(x, y, z)]
        .Select(it => it.GetComponent<IStageObjectView>())
        .NotNull()
        .Select(ToModel)
        .OfType<T>()
        .ToImmutableList();
  }

  private int ToIndex(int x, int y, int z) =>
    x + y * size.x + z * size.x * size.y;

  private static (int x, int y, int z) FromIndex(int index, Vector3Int size) =>
    (index % size.x, index / size.x % size.y, index / (size.x * size.y));

  internal IStageObject ToModel(IStageObjectView view) => view switch
  {
    BoxView box => new CollidableMovableStageObjectAdapter(this, box),
    PlayerView player => new MovableStageObjectAdapter(this, player),
    GoalView => new GoalStageObjectAdapter(),
    _ => throw new NotSupportedException(
      $"GameStage does not know how to translate {view.GetType()} into a {nameof(IStageObject)}."
    ),
  };

  private class MovableStageObjectAdapter : IMovableStageObject
  {
    private readonly GameStage stage;
    private readonly IMovableView view;

    public MovableStageObjectAdapter(GameStage stage, IMovableView view)
    {
      this.stage = stage;
      this.view = view;
    }

    public async UniTask MoveTo(
      int x,
      int y,
      int z,
      MoveMode moveMode,
      CancellationToken ct
    )
    {
      await view.MoveTo(x, y, z, moveMode, ct);
      // Set the cell state in [stage], but not implemented yet
    }
  }

  private sealed class CollidableMovableStageObjectAdapter :
    MovableStageObjectAdapter,
    ICollidableStageObject
  {
    public CollidableMovableStageObjectAdapter(GameStage stage, IMovableView view)
      : base(stage, view) { }
  }

  private sealed class GoalStageObjectAdapter : IGoalStageObject { }
}
