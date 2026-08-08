using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class TriangleMesh : MonoBehaviour
{
  [SerializeField]
  private float width = 0.3f;

  [SerializeField]
  private float length = 0.4f;

  private void Awake() => Rebuild();

  private void OnEnable() => Rebuild();

  private void OnValidate() => Rebuild();

  private void Rebuild()
  {
    var meshFilter = GetComponent<MeshFilter>();
    if (meshFilter == null)
    {
      return;
    }

    var mesh = new Mesh
    {
      name = nameof(TriangleMesh),
      hideFlags = HideFlags.DontSave,
    };
    mesh.vertices = new[]
    {
      new Vector3(-width / 2f, 0f, 0f),
      new Vector3(width / 2f, 0f, 0f),
      new Vector3(0f, 0f, length),
    };
    mesh.uv = new[]
    {
      new Vector2(0f, 0f),
      new Vector2(1f, 0f),
      new Vector2(0.5f, 1f),
    };
    mesh.triangles = new[] { 0, 2, 1 };
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    meshFilter.sharedMesh = mesh;
  }
}
