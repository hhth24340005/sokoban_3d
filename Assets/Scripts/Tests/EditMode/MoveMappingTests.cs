using NUnit.Framework;

public class MoveMappingTests
{
  [TestCase(0f, Direction.Forward, Direction.Right, Direction.Back, Direction.Left)]
  [TestCase(44f, Direction.Forward, Direction.Right, Direction.Back, Direction.Left)]
  [TestCase(46f, Direction.Right, Direction.Back, Direction.Left, Direction.Forward)]
  [TestCase(90f, Direction.Right, Direction.Back, Direction.Left, Direction.Forward)]
  [TestCase(180f, Direction.Back, Direction.Left, Direction.Forward, Direction.Right)]
  [TestCase(270f, Direction.Left, Direction.Forward, Direction.Right, Direction.Back)]
  [TestCase(360f, Direction.Forward, Direction.Right, Direction.Back, Direction.Left)]
  [TestCase(-90f, Direction.Left, Direction.Forward, Direction.Right, Direction.Back)]
  [TestCase(-1f, Direction.Forward, Direction.Right, Direction.Back, Direction.Left)]
  public void SnapsYawToNearestQuadrant(
    float yawDegrees,
    Direction forward,
    Direction right,
    Direction back,
    Direction left
  )
  {
    var mapping = MoveMapping.FromYawDegrees(yawDegrees);
    Assert.AreEqual(forward, mapping.Forward);
    Assert.AreEqual(right, mapping.Right);
    Assert.AreEqual(back, mapping.Back);
    Assert.AreEqual(left, mapping.Left);
  }

  [Test]
  public void EqualMappingsCompareEqual()
  {
    var a = MoveMapping.FromYawDegrees(10f);
    var b = MoveMapping.FromYawDegrees(20f);
    Assert.AreEqual(a, b);
    Assert.IsTrue(a == b);
  }

  [Test]
  public void DifferentMappingsCompareNotEqual()
  {
    var a = MoveMapping.FromYawDegrees(0f);
    var b = MoveMapping.FromYawDegrees(90f);
    Assert.AreNotEqual(a, b);
    Assert.IsTrue(a != b);
  }

  [TestCase(0f, Direction.Forward, ScreenDirection.Up)]
  [TestCase(0f, Direction.Right, ScreenDirection.Right)]
  [TestCase(0f, Direction.Back, ScreenDirection.Down)]
  [TestCase(0f, Direction.Left, ScreenDirection.Left)]
  [TestCase(90f, Direction.Right, ScreenDirection.Up)]
  [TestCase(90f, Direction.Back, ScreenDirection.Right)]
  [TestCase(90f, Direction.Left, ScreenDirection.Down)]
  [TestCase(90f, Direction.Forward, ScreenDirection.Left)]
  public void ScreenDirectionForIsTheInverseOfToWorld(
    float yawDegrees,
    Direction worldDirection,
    ScreenDirection expected
  )
  {
    var mapping = MoveMapping.FromYawDegrees(yawDegrees);
    Assert.AreEqual(expected, mapping.ScreenDirectionFor(worldDirection));
  }

  [Test]
  public void ScreenDirectionForRoundTripsWithToWorld()
  {
    var mapping = MoveMapping.FromYawDegrees(135f);
    foreach (var screenDirection in new[]
    {
      ScreenDirection.Up,
      ScreenDirection.Down,
      ScreenDirection.Left,
      ScreenDirection.Right,
    })
    {
      var worldDirection = mapping.ToWorld(screenDirection);
      Assert.AreEqual(screenDirection, mapping.ScreenDirectionFor(worldDirection));
    }
  }
}
