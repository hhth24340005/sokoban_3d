using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum MoveMode
{
  Slide,
  Fall,
}

public interface IStageObject
{
  Vector3Int Position { get; }
}

public interface IMovableStageObject : IStageObject
{
  public UniTask MoveTo(int x, int y, int z, MoveMode moveMode, CancellationToken ct);
}

public interface IControllableStageObject : IMovableStageObject { }

public interface ICollidableStageObject : IStageObject { }

public interface ISubjectBoxStageObject : IMovableStageObject, ICollidableStageObject { }

public interface IGoalStageObject : IStageObject { }
