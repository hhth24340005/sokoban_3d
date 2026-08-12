using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GameStage
{
  private readonly (int x, int y, int z) size;
  private readonly List<List<List<List<EntityId>>>> cells;

  public GameStage((int x, int y, int z) size, IReadOnlyDictionary<EntityId, Position> entityPosition)
  {
    this.size = size;
    cells =
      Enumerable.Range(0, size.x)
        .Select(_ =>
          Enumerable.Range(0, size.y)
            .Select(_ =>
              Enumerable.Range(0, size.z)
                .Select(_ => new List<EntityId>())
                .ToList()
            ).ToList()
        ).ToList();
    foreach ((var id, (var x, var y, var z)) in entityPosition)
    {
      cells[x][y][z].Add(id);
    }
  }

  public IReadOnlyList<Movement> MovePlayers(Direction direction)
  {
    throw new Exception();
  }

  public sealed record Movement(
    EntityId Who,
    Position From,
    Position To
  );

  public readonly struct EntityId : IEquatable<EntityId>
  {
    private readonly int identity;

    public EntityId(int identity)
    {
      this.identity = identity;
    }

    public bool Equals(EntityId other) => identity == other.identity;
  }

  public sealed record Position(int X, int Y, int Z);

  public enum Direction
  {
    Forward,
    Right,
    Back,
    Left,
  }
}
