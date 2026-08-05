using System.Threading;
using Cysharp.Threading.Tasks;

public enum MoveMode
{
  Pushed,
  Fall,
}

public interface IStageObject { }

public interface IMovableStageObject : IStageObject
{
  public UniTask MoveTo(int x, int y, int z, MoveMode moveMode, CancellationToken ct);
}

public interface ICollidableStageObject : IStageObject { }

public interface ISubjectBoxStageObject : IMovableStageObject, ICollidableStageObject { }

public interface IGoalStageObject : IStageObject { }
