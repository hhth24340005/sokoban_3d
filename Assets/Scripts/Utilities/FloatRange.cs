using System;
using UnityEngine;

[Serializable]
public struct FloatRange
{
  [SerializeField]
  private float a;

  [SerializeField]
  private float b;

  public FloatRange(float a, float b)
  {
    this.a = a;
    this.b = b;
  }

  public readonly float Min => Mathf.Min(a, b);

  public readonly float Max => Mathf.Max(a, b);

  public readonly float Clamp(float it) => Mathf.Clamp(value: it, min: Min, max: Max);

  public readonly float Lerp(float rate) => Mathf.Lerp(a: Min, b: Max, t: rate);
}
