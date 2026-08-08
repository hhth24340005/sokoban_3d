using NUnit.Framework;
using UnityEngine;

public class OrbitCameraTests
{
  private const float MinPitchDegrees = 25f;
  private const float MaxPitchDegrees = 70f;
  private const float MinDistance = 3f;
  private const float MaxDistance = 15f;

  private static OrbitCamera Create(
    float yawDegrees,
    float pitchDegrees,
    float distance,
    Vector3? pivot = null
  ) =>
    new(
      pivot ?? Vector3.zero,
      yawDegrees,
      pitchDegrees,
      distance,
      MinPitchDegrees,
      MaxPitchDegrees,
      MinDistance,
      MaxDistance
    );

  [TestCase(0f, MinPitchDegrees)]
  [TestCase(MinPitchDegrees - 1f, MinPitchDegrees)]
  [TestCase(MaxPitchDegrees + 1f, MaxPitchDegrees)]
  [TestCase(45f, 45f)]
  public void PitchIsClampedToRange(float input, float expected)
  {
    var camera = Create(0f, input, MinDistance);
    Assert.AreEqual(expected, camera.PitchDegrees);
  }

  [TestCase(MinDistance - 1f, MinDistance)]
  [TestCase(MaxDistance + 1f, MaxDistance)]
  [TestCase(6f, 6f)]
  public void DistanceIsClampedToRange(float input, float expected)
  {
    var camera = Create(0f, MinPitchDegrees, input);
    Assert.AreEqual(expected, camera.Distance);
  }

  [TestCase(0f, 0f)]
  [TestCase(360f, 0f)]
  [TestCase(-90f, 270f)]
  [TestCase(-1f, 359f)]
  [TestCase(450f, 90f)]
  public void YawWrapsIntoZeroTo360Range(float input, float expected)
  {
    var camera = Create(input, MinPitchDegrees, MinDistance);
    Assert.AreEqual(expected, camera.YawDegrees, 1e-3f);
  }

  [Test]
  public void RotatedAccumulatesYawAndPitch()
  {
    var camera = Create(10f, 40f, MinDistance);
    var rotated = camera.Rotated(20f, 5f);
    Assert.AreEqual(30f, rotated.YawDegrees, 1e-3f);
    Assert.AreEqual(45f, rotated.PitchDegrees, 1e-3f);
  }

  [Test]
  public void ZoomedAccumulatesDistanceWithinClamp()
  {
    var camera = Create(0f, MinPitchDegrees, 5f);
    var zoomed = camera.Zoomed(2f);
    Assert.AreEqual(7f, zoomed.Distance, 1e-3f);
  }

  [Test]
  public void PositionLooksTowardPivot()
  {
    var pivot = new Vector3(2f, 0f, 2f);
    var camera = Create(0f, 45f, 5f, pivot);
    var toPivot = (pivot - camera.Position).normalized;
    var forward = camera.Rotation * Vector3.forward;
    Assert.Less(Vector3.Distance(toPivot, forward), 1e-3f);
  }
}
