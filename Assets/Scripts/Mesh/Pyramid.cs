using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class Pyramid : MonoBehaviour
{
  public static readonly Vector3 v0 = Vector3.zero;

  public static readonly Vector3 v1 = Vector3.right;

  [SerializeField]
  private Vector3 v2;
  public Vector3 V2
  {
    get => v2;
    set
    {
      v2 = value;
      UpdateMesh();
    }
  }

  [SerializeField]
  private Vector3 v3;
  public Vector3 V3
  {
    get => v3;
    set
    {
      v3 = value;
      UpdateMesh();
    }
  }

  [SerializeField]
  private Vector3 meshBase;
  public Vector3 MeshBase
  {
    get => meshBase;
    set
    {
      meshBase = value;
      UpdateMesh();
    }
  }

  public Pyramid() => Reset();

  private void Reset()
  {
    var half = 1f / 2;
    var baseHeight = Mathf.Sin(60f * Mathf.Deg2Rad);
    var baseIncenterHeight = half * Mathf.Tan(60f / 2 * Mathf.Deg2Rad);
    var apexHeight =
      Mathf.Sqrt(1f - (half * half + baseIncenterHeight * baseIncenterHeight));

    meshBase = new Vector3(half, 0f, baseIncenterHeight);
    v2 = new Vector3(half, 0f, baseHeight);
    v3 = new Vector3(half, apexHeight, baseIncenterHeight);
  }

  private void OnValidate() => UpdateMesh();

  private void Start() => UpdateMesh();

  private void UpdateMesh()
  {
    (var corners, var faces) = TriangleVerticesOf();
    var vertices = new Vector3[corners.Length * 3];
    var indices = Enumerable.Range(0, corners.Length * 3).ToArray();
    foreach (var index in indices)
    {
      vertices[index] = corners[faces[index]];
    }

    var mesh =
      new Mesh()
      {
        vertices = vertices,
        triangles = indices,
      };
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();
    GetComponent<MeshFilter>().mesh = mesh;
  }

  private (Vector3[] corners, int[] faces) TriangleVerticesOf()
  {
    var corners = new[] { v0, v1, V2, V3 }.Select(it => it - meshBase).ToArray();
    var indices = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 1, 1, 3, 2 };
    if (0f < Vector3.Dot(Vector3.Cross(v1 - v0, V2 - v0), V3 - v0))
    {
      for (var i = 0; i < indices.Length; i += 3)
      {
        (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
      }
    }
    return (corners, indices);
  }
}
