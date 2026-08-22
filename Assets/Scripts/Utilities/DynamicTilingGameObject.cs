using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DynamicTilingCube : MonoBehaviour
{
  private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

  [SerializeField]
  private float repeatEvery = 1f;

  [SerializeField]
  private List<Renderer> renderersX;

  [SerializeField]
  private List<Renderer> renderersY;

  [SerializeField]
  private List<Renderer> renderersZ;

  private void Start()
  {
    var w = transform.lossyScale.x / repeatEvery;
    var h = transform.lossyScale.y / repeatEvery;
    var d = transform.lossyScale.z / repeatEvery;
    var block = new MaterialPropertyBlock();
    new[]
    {
      (renderersX, (d, h)),
      (renderersY, (w, d)),
      (renderersZ, (w, h)),
    }.ToList()
    .ForEach(it =>
    {
      var (renderers, (x, y)) = it;
      foreach (var renderer in renderers)
      {
        renderer.GetPropertyBlock(block);
        block.SetVector(BaseMapSt, new(x, y, 0f, 0f));
        renderer.SetPropertyBlock(block);
      }
    });
  }
}
