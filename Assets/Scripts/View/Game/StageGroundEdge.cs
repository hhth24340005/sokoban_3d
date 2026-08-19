using System.Collections.Generic;
using UnityEngine;

public sealed class StageGroundEdge : MonoBehaviour
{
  [SerializeField]
  private List<Transform> edges;

  public void Build((float x, float z) center, (float x, float z) size)
  {
    {
      transform.localPosition = new(center.x, 0f, center.z);
      var scale = transform.localScale;
      scale.x *= size.x;
      scale.z *= size.z;
      transform.localScale = scale;
    }
    foreach (var edge in edges)
    {
      var scaleX = (edge.lossyScale.x + edge.localScale.z * 2f) / edge.lossyScale.x;
      var scaleZ = edge.localScale.z * edge.localScale.z / edge.lossyScale.z;
      var pos = edge.localPosition;
      pos.z = scaleZ / 2f;
      edge.localPosition = pos;
      edge.localScale = new(scaleX, edge.localScale.y, scaleZ);
    }
  }
}
