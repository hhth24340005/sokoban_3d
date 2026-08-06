using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class StageController
{
  private readonly GameStage stage;

  public StageController(GameStage stage)
  {
    this.stage = stage;
  }

  public async UniTask Step(Direction direction, CancellationToken ct)
  {
    var offset = ToOffset(direction);
    var moves = new List<(IMovableStageObject movable, Vector3Int target)>();
    foreach (var controllable in stage.GetAll<IControllableStageObject>())
    {
      TryPlanStep(controllable, offset, moves);
    }

    var animations = new List<UniTask>(moves.Count);
    foreach (var (movable, target) in moves)
    {
      animations.Add(movable.MoveTo(target.x, target.y, target.z, MoveMode.Slide, ct));
    }
    await UniTask.WhenAll(animations);
  }

  private void TryPlanStep(
    IControllableStageObject controllable,
    Vector3Int offset,
    List<(IMovableStageObject movable, Vector3Int target)> moves
  )
  {
    var target = controllable.Position + offset;
    if (!stage.InBounds(target.x, target.y, target.z))
    {
      return;
    }

    var obstacles = stage.Get<ICollidableStageObject>(target.x, target.y, target.z);
    if (obstacles.Count == 0)
    {
      moves.Add((controllable, target));
      return;
    }

    var beyond = target + offset;
    if (!stage.InBounds(beyond.x, beyond.y, beyond.z))
    {
      return;
    }
    if (stage.Get<ICollidableStageObject>(beyond.x, beyond.y, beyond.z).Count != 0)
    {
      return;
    }

    var pushed = new List<IMovableStageObject>(obstacles.Count);
    foreach (var obstacle in obstacles)
    {
      if (obstacle is not IMovableStageObject movable)
      {
        return;
      }
      pushed.Add(movable);
    }

    foreach (var movable in pushed)
    {
      moves.Add((movable, beyond));
    }
    moves.Add((controllable, target));
  }

  private static Vector3Int ToOffset(Direction direction) => direction switch
  {
    Direction.Right => Vector3Int.right,
    Direction.Left => Vector3Int.left,
    Direction.Up => Vector3Int.up,
    Direction.Down => Vector3Int.down,
    Direction.Forward => Vector3Int.forward,
    Direction.Back => Vector3Int.back,
    _ => throw new NotSupportedException($"Unknown direction {direction}."),
  };
}
