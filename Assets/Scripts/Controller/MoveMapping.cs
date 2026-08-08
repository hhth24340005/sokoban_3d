using System;
using UnityEngine;

public readonly struct MoveMapping : IEquatable<MoveMapping>
{
  public Direction Forward { get; }
  public Direction Right { get; }
  public Direction Back { get; }
  public Direction Left { get; }

  public MoveMapping(Direction forward, Direction right, Direction back, Direction left)
  {
    Forward = forward;
    Right = right;
    Back = back;
    Left = left;
  }

  private static readonly Direction[] clockwise =
  {
    Direction.Forward,
    Direction.Right,
    Direction.Back,
    Direction.Left,
  };

  public static MoveMapping FromYawDegrees(float yawDegrees)
  {
    var offset = SnapIndex(yawDegrees);
    return new MoveMapping(
      clockwise[offset],
      clockwise[(offset + 1) % 4],
      clockwise[(offset + 2) % 4],
      clockwise[(offset + 3) % 4]
    );
  }

  private static int SnapIndex(float yawDegrees)
  {
    var normalized = yawDegrees % 360f;
    if (normalized < 0f)
    {
      normalized += 360f;
    }
    return Mathf.RoundToInt(normalized / 90f) % 4;
  }

  public Direction ToWorld(ScreenDirection screenDirection) => screenDirection switch
  {
    ScreenDirection.Up => Forward,
    ScreenDirection.Down => Back,
    ScreenDirection.Left => Left,
    ScreenDirection.Right => Right,
    _ => throw new NotSupportedException($"Unknown screen direction {screenDirection}."),
  };

  public bool Equals(MoveMapping other) =>
    Forward == other.Forward &&
    Right == other.Right &&
    Back == other.Back &&
    Left == other.Left;

  public override bool Equals(object obj) => obj is MoveMapping other && Equals(other);

  public override int GetHashCode() => HashCode.Combine(Forward, Right, Back, Left);

  public static bool operator ==(MoveMapping left, MoveMapping right) => left.Equals(right);

  public static bool operator !=(MoveMapping left, MoveMapping right) => !left.Equals(right);
}
