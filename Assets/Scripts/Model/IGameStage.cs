#nullable enable

using System.Collections.Generic;

public interface IGameStage
{
  public IReadOnlyList<T> Get<T>(int x, int y, int z);
}
