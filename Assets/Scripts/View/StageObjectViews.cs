using System.Threading;
using Cysharp.Threading.Tasks;

public interface IStageObjectView
{
  void TeleportTo(int x, int y, int z);
}

public interface IMovableView : IStageObjectView
{
  public UniTask MoveTo(int x, int y, int z, MoveMode moveMode, CancellationToken ct);
}
