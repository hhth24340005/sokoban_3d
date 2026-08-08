using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class StageController
{
  private readonly GameStage stage;
  private readonly Stack<List<Move>> undoStack = new();
  private readonly Stack<List<Move>> redoStack = new();

  public StageController(GameStage stage)
  {
    this.stage = stage;
  }

  public bool CanUndo => undoStack.Count > 0;

  public bool CanRedo => redoStack.Count > 0;

  public bool IsCleared() =>
    stage
      .GetAll<IGoalStageObject>()
      .All(goal =>
        stage
          .Get<ISubjectBoxStageObject>(
            goal.Position.x,
            goal.Position.y,
            goal.Position.z
          ).Count > 0
      );

  public async UniTask Step(Direction direction, CancellationToken ct)
  {
    var offset = ToOffset(direction);
    var moves = new List<Move>();
    foreach (var controllable in stage.GetAll<IControllableStageObject>())
    {
      TryPlanStep(controllable, offset, moves);
    }

    if (moves.Count == 0)
    {
      await UniTask.Yield(ct);
      return;
    }

    await Apply(moves, ct);
    undoStack.Push(moves);
    redoStack.Clear();
  }

  public async UniTask Undo(CancellationToken ct)
  {
    if (undoStack.Count == 0)
    {
      await UniTask.Yield(ct);
      return;
    }

    var moves = undoStack.Pop();
    await ApplyReverse(moves, ct);
    redoStack.Push(moves);
  }

  public async UniTask Redo(CancellationToken ct)
  {
    if (redoStack.Count == 0)
    {
      await UniTask.Yield(ct);
      return;
    }

    var moves = redoStack.Pop();
    await Apply(moves, ct);
    undoStack.Push(moves);
  }

  private static UniTask Apply(List<Move> moves, CancellationToken ct) =>
    UniTask.WhenAll(
      moves.Select(move =>
        move.Subject.MoveTo(move.To.x, move.To.y, move.To.z, move.Mode, ct)
      )
    );

  private static UniTask ApplyReverse(List<Move> moves, CancellationToken ct) =>
    UniTask.WhenAll(
      moves.Select(move =>
        move.Subject.MoveTo(move.From.x, move.From.y, move.From.z, move.Mode, ct)
      )
    );

  private void TryPlanStep(
    IControllableStageObject controllable,
    Vector3Int offset,
    List<Move> moves
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
      moves.Add(new Move(controllable, controllable.Position, target, MoveMode.Slide));
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
      moves.Add(new Move(movable, movable.Position, beyond, MoveMode.Slide));
    }
    moves.Add(new Move(controllable, controllable.Position, target, MoveMode.Slide));
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

  private readonly struct Move
  {
    public IMovableStageObject Subject { get; }
    public Vector3Int From { get; }
    public Vector3Int To { get; }
    public MoveMode Mode { get; }

    public Move(IMovableStageObject subject, Vector3Int from, Vector3Int to, MoveMode mode)
    {
      Subject = subject;
      From = from;
      To = to;
      Mode = mode;
    }
  }
}
