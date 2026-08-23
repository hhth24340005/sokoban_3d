using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

public sealed class GameStage
{
  private readonly (int x, int y, int z) _size;
  private readonly List<Entity> _sortedEntities;
  private readonly ISet<(TypeId, Rule)> _rules;
  private readonly Stack<
    IReadOnlyDictionary<Entity, IReadOnlyList<Movement>>
  > _moveHistory = new();
  private readonly Stack<
    IReadOnlyDictionary<Entity, IReadOnlyList<Movement>>
  > _undoHistory = new();

  public bool IsCleared
  {
    get
    {
      var goals = ListEntitiesWithRule(Rule.Goal).ToImmutableList();
      if (goals.Count == 0)
      {
        return false;
      }
      return goals.All(goal =>
      {
        TryGetCellAt(goal.CurrentPos, out var cell);
        return cell.Any(it => it.HasRule(Rule.Key));
      });
    }
  }

  public bool CanUndo => 0 < _moveHistory.Count;

  public bool CanRedo => 0 < _undoHistory.Count;

  public GameStage(
    (int x, int y, int z) size,
    IReadOnlyDictionary<EntityId, (TypeId, Position)> entityData,
    IReadOnlyDictionary<TypeId, IReadOnlyCollection<Rule>> typeData
  )
  {
    _size = size;
    _sortedEntities =
      entityData.Select(kv =>
      {
        var (id, (type, pos)) = kv;
        return new Entity(this, id, type, pos);
      }).OrderBy(x => x)
      .ToList();
    _rules =
      typeData
        .SelectMany(kv => kv.Value.Distinct().Select(rule => (kv.Key, rule)))
        .ToHashSet();
  }

  public IReadOnlyDictionary<EntityId, IReadOnlyList<Movement>> MovePlayers(
    Direction direction)
  {
    var moves =
      ListEntitiesWithRule(Rule.Controllable)
        .SelectMany(player =>
        {
          var playerTargetPos = PositionOffset(player.CurrentPos, direction);
          if (!TryGetCellAt(playerTargetPos,
                out var playerTargetCellEnumerable))
            return Enumerable.Empty<(Entity, Position, Position)>();
          var playerTargetCell = playerTargetCellEnumerable.ToImmutableList();
          if (playerTargetCell.Any(it => it.HasRule(Rule.Stop)))
          {
            return ClimbOrNothing(player, playerTargetPos);
          }

          var pushable =
            playerTargetCell
              .Where(it => it.HasRule(Rule.Pushable))
              .ToImmutableList();
          if (pushable.IsEmpty)
          {
            return new[] { (player, player.CurrentPos, playerTargetPos) };
          }

          var pushTargetPos = PositionOffset(playerTargetPos, direction);
          if (!TryGetCellAt(pushTargetPos, out var pushTargetCell) ||
              IsBlocked(pushTargetCell))
          {
            return ClimbOrNothing(player, playerTargetPos);
          }

          var playerTargetUpPos =
            playerTargetPos with { Y = playerTargetPos.Y + 1 };
          if (TryGetCellAt(playerTargetUpPos, out var playerTargetUpCell) &&
              playerTargetUpCell.Any(it => it.HasRule(Rule.Gravitational)))
          {
            return ClimbOrNothing(player, playerTargetPos);
          }

          var pushed = new List<(Entity, Position, Position)>
          {
            (player, player.CurrentPos, playerTargetPos)
          };
          pushed.AddRange(pushable.Select(it =>
            (it, playerTargetPos, pushTargetPos)));
          return pushed;
        }).ToImmutableList();

    var paths = new Dictionary<Entity, List<Movement>>();
    foreach (var (entity, from, to) in moves)
    {
      entity.MoveToOrThrow(to);
      AppendMovement(paths, entity, from, to);
    }

    foreach (var (entity, from, to) in FallGravitational())
    {
      AppendMovement(paths, entity, from, to);
    }

    if (paths.Count <= 0)
    {
      return ImmutableDictionary<EntityId, IReadOnlyList<Movement>>.Empty;
    }

    var lockedPaths = paths.ToImmutableDictionary(
      it => it.Key,
      it => (IReadOnlyList<Movement>)it.Value.ToImmutableList()
    );

    _moveHistory.Push(lockedPaths);
    _undoHistory.Clear();
    return ToMovementPaths(lockedPaths);
  }

  public IReadOnlyDictionary<EntityId, IReadOnlyList<Movement>> Undo()
  {
    if (!CanUndo)
    {
      return ImmutableDictionary<EntityId, IReadOnlyList<Movement>>.Empty;
    }
    var undoing = _moveHistory.Pop();
    _undoHistory.Push(undoing);
    foreach (var (entity, path) in undoing)
    {
      entity.MoveToOrThrow(path[0].From);
    }
    return ToMovementPaths(undoing);
  }

  public IReadOnlyDictionary<EntityId, IReadOnlyList<Movement>> Redo()
  {
    if (!CanRedo)
    {
      return ImmutableDictionary<EntityId, IReadOnlyList<Movement>>.Empty;
    }
    var redoing = _undoHistory.Pop();
    _moveHistory.Push(redoing);
    foreach (var (entity, path) in redoing)
    {
      entity.MoveToOrThrow(path[^1].To);
    }
    return ToMovementPaths(redoing);
  }

  public sealed record Movement(
    Position From,
    Position To
  );

  public readonly struct EntityId : IEquatable<EntityId>
  {
    private readonly int _identity;

    public EntityId(int identity)
    {
      this._identity = identity;
    }

    public bool Equals(EntityId other) => _identity == other._identity;
  }

  public readonly struct TypeId : IEquatable<TypeId>
  {
    private readonly int _identity;

    public TypeId(int identity)
    {
      this._identity = identity;
    }

    public bool Equals(TypeId other) => _identity == other._identity;
  }

  public sealed record Position(int X, int Y, int Z);

  public enum Direction
  {
    PlusZ,
    PlusX,
    MinusZ,
    MinusX,
  }

  public enum Rule
  {
    Stop,
    Controllable,
    Pushable,
    Gravitational,
    Key,
    Goal
  }

  private IEnumerable<(Entity, Position, Position)> ClimbOrNothing(
    Entity player,
    Position climbOverPos
  )
  {
    var headPos = player.CurrentPos with { Y = player.CurrentPos.Y + 1 };
    if (!TryGetCellAt(headPos, out var headCell) || IsBlocked(headCell))
    {
      return Enumerable.Empty<(Entity, Position, Position)>();
    }

    var climbToPos = climbOverPos with { Y = climbOverPos.Y + 1 };
    if (!TryGetCellAt(climbToPos, out var climbToCell) || IsBlocked(climbToCell))
    {
      return Enumerable.Empty<(Entity, Position, Position)>();
    }

    return new[] { (player, player.CurrentPos, climbToPos) };
  }

  private static bool IsBlocked(IEnumerable<Entity> cell) =>
    cell.Any(it => it.HasRule(Rule.Stop) || it.HasRule(Rule.Pushable));

  private static void AppendMovement(
    IDictionary<Entity, List<Movement>> paths,
    Entity entity,
    Position from,
    Position to
  )
  {
    if (!paths.TryGetValue(entity, out var path))
    {
      path = new List<Movement>();
      paths.Add(entity, path);
    }
    path.Add(new Movement(from, to));
  }

  private static IReadOnlyDictionary<EntityId, IReadOnlyList<Movement>> ToMovementPaths(
    IReadOnlyDictionary<Entity, IReadOnlyList<Movement>> paths
  ) =>
    paths.ToImmutableDictionary(
      kv => kv.Key.Id,
      kv => (IReadOnlyList<Movement>)kv.Value.ToImmutableList()
    );

  private IEnumerable<(Entity, Position, Position)> FallGravitational() =>
    ListEntitiesWithRule(Rule.Gravitational)
      .Select(entity =>
      {
        var fallTo = entity.CurrentPos;
        while (true)
        {
          var candidate = fallTo with { Y = fallTo.Y - 1 };
          if (TryGetCellAt(candidate, out var cell) && !IsBlocked(cell))
          {
            fallTo = candidate;
          }
          else
          {
            break;
          }
        }
        return (entity, fallTo);
      }).ToImmutableList()
      .Select(mv =>
      {
        var from = mv.entity.CurrentPos;
        var to = mv.fallTo;
        mv.entity.MoveToOrThrow(mv.fallTo);
        return (mv.entity, from, to);
      })
      .Where(it => it.from != it.to)
      .ToImmutableList();

  private bool TryGetCellAt(Position pos, out IEnumerable<Entity> cell)
  {
    if (IsOutOfBounds(pos))
    {
      cell = null;
      return false;
    }
    cell = new Cell(pos, _sortedEntities);
    return true;
  }

  private IEnumerable<Entity> ListEntitiesWithRule(Rule rule) =>
    _sortedEntities.Where(it => it.HasRule(rule));

  private bool IsOutOfBounds(Position pos) =>
    pos.X < 0 || _size.x <= pos.X ||
    pos.Y < 0 || _size.y <= pos.Y ||
    pos.Z < 0 || _size.z <= pos.Z;

  private Position PositionOffset(Position pos, Direction direction) =>
    direction switch
    {
      Direction.PlusZ => pos with { Z = pos.Z + 1 },
      Direction.PlusX => pos with { X = pos.X + 1 },
      Direction.MinusZ => pos with { Z = pos.Z - 1 },
      Direction.MinusX => pos with { X = pos.X - 1 },
      _ => throw new Exception($"Unknown {nameof(Direction)} type >.<"),
    };

  private sealed class Entity : IComparable<Entity>, IComparable<Position>
  {
    public EntityId Id { get; }
    public Position CurrentPos { get; private set; }
    private readonly TypeId _type;
    private readonly GameStage _outer;

    public Entity(GameStage outer, EntityId id, TypeId type, Position currentPos)
    {
      Id = id;
      CurrentPos = currentPos;
      this._outer = outer;
      this._type = type;
    }

    public void MoveToOrThrow(Position target)
    {
      if (_outer.IsOutOfBounds(target))
      {
        throw new InvalidOperationException();
      }
      if (CurrentPos == target)
      {
        return;
      }
      CurrentPos = target;
      _outer._sortedEntities.Sort();
    }

    public bool HasRule(Rule rule) => _outer._rules.Contains((_type, rule));

    public int CompareTo(Entity other) => CompareTo(other.CurrentPos);

    public int CompareTo(Position other) => FlatPos(CurrentPos).CompareTo(FlatPos(other));

    private int FlatPos(Position pos) =>
      pos.X +
      pos.Y * _outer._size.x +
      pos.Z * _outer._size.x * _outer._size.y;
  }

  private class Cell : IEnumerable<Entity>
  {
    private readonly Position _pos;
    private readonly IReadOnlyList<Entity> _entities;

    public Cell(Position pos, IReadOnlyList<Entity> entities)
    {
      this._pos = pos;
      this._entities = entities;
    }

    public IEnumerator<Entity> GetEnumerator()
    {
      var start = SearchLower();
      return _entities.Skip(start).TakeWhile(x => x.CompareTo(_pos) == 0).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private int SearchLower()
    {
      var low = 0;
      var high = _entities.Count;
      while (low < high)
      {
        var mid = (low + high) / 2;
        if (_entities[mid].CompareTo(_pos) < 0)
        {
          low = mid + 1;
        }
        else
        {
          high = mid;
        }
      }
      return low;
    }
  }
}
