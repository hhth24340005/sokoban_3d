using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class StageGround : MonoBehaviour
{
  [SerializeField]
  private List<GameObject> checkerPrefabs = new();

  [SerializeField]
  private StageGroundEdge edge;

  // ===

  public void Build(Vector3Int size)
  {
    var platform = transform.CreateChild("Platform").GameObject.transform;
    var checkers = platform.CreateChild("Checkers").GameObject.transform;
    BuildFloor(checkers, size);
    edge.Build(CenterOf(size), (size.x, size.z));
  }

  // ===

  private static (float x, float z) CenterOf(Vector3Int size) =>
    ((size.x - 1) / 2f, (size.z - 1) / 2f);

  private void BuildFloor(Transform parent, Vector3Int size)
  {
    if (checkerPrefabs.Count == 0)
    {
      throw new IndexOutOfRangeException();
    }
    foreach (var x in Enumerable.Range(0, size.x))
    {
      foreach (var z in Enumerable.Range(0, size.z))
      {
        var prefab = checkerPrefabs[(x + (z % checkerPrefabs.Count)) % checkerPrefabs.Count];
        var created = parent.CreateChild(prefab, setActive: true).GameObject;
        created.transform.localPosition = new(x, 0f, z);
      }
    }
  }
}
