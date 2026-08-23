using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SdfBaker
{
  private const int OutputLongestSide = 256;
  private const float PaddingRatio = 0.125f;
  private const int Supersample = 4;
  private const string MenuPath = "Assets/Bake SDF from Sprite";

  [MenuItem(MenuPath, true)]
  private static bool ValidateBake() => SelectedSprites().Any();

  [MenuItem(MenuPath)]
  private static void Bake()
  {
    foreach (var sprite in SelectedSprites())
    {
      BakeOne(sprite);
    }
    AssetDatabase.Refresh();
  }

  private static IEnumerable<Sprite> SelectedSprites() =>
    Selection.objects
      .Select(AssetDatabase.GetAssetPath)
      .Where(path => !string.IsNullOrEmpty(path))
      .Distinct()
      .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
      .OfType<Sprite>();

  private static void BakeOne(Sprite sprite)
  {
    var bounds = sprite.bounds;
    var padding = Mathf.Max(bounds.size.x, bounds.size.y) * PaddingRatio;
    var paddedSize = new Vector2(
      bounds.size.x + padding * 2f,
      bounds.size.y + padding * 2f
    );

    var pixelsPerUnit = OutputLongestSide / Mathf.Max(paddedSize.x, paddedSize.y);
    var width = Mathf.Max(1, Mathf.RoundToInt(paddedSize.x * pixelsPerUnit));
    var height = Mathf.Max(1, Mathf.RoundToInt(paddedSize.y * pixelsPerUnit));
    var spread = padding * pixelsPerUnit * Supersample;

    var mask = RenderMask(sprite, bounds, padding, width * Supersample, height * Supersample);
    var distance = SignedDistance(mask, width * Supersample, height * Supersample);
    var pixels = Downsample(distance, width, height, spread);

    var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
    texture.SetPixels(pixels);
    texture.Apply();

    var path = Path.ChangeExtension(AssetDatabase.GetAssetPath(sprite), null) + "_sdf.png";
    File.WriteAllBytes(path, texture.EncodeToPNG());
    Object.DestroyImmediate(texture);

    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    ApplyImportSettings(path, pixelsPerUnit);

    Debug.Log(
      $"Baked {path} — {width}x{height}, {pixelsPerUnit:F1} px/unit, " +
      $"spread {padding:F3} units ({paddedSize.x:F3} x {paddedSize.y:F3} world size)"
    );
  }

  private static bool[] RenderMask(
    Sprite sprite,
    Bounds bounds,
    float padding,
    int width,
    int height
  )
  {
    var vertices = sprite.vertices;
    var mesh = new Mesh
    {
      hideFlags = HideFlags.HideAndDontSave,
      vertices = vertices.Select(it => (Vector3)it).ToArray(),
      colors = Enumerable.Repeat(Color.white, vertices.Length).ToArray(),
      triangles = sprite.triangles.Select(it => (int)it).ToArray(),
    };

    var material = new Material(Shader.Find("Hidden/Internal-Colored"))
    {
      hideFlags = HideFlags.HideAndDontSave
    };
    material.SetInt("_SrcBlend", (int)BlendMode.One);
    material.SetInt("_DstBlend", (int)BlendMode.Zero);
    material.SetInt("_Cull", (int)CullMode.Off);
    material.SetInt("_ZWrite", 0);
    material.SetInt("_ZTest", (int)CompareFunction.Always);
    material.SetColor("_Color", Color.white);

    var target = RenderTexture.GetTemporary(
      width,
      height,
      0,
      RenderTextureFormat.ARGB32,
      RenderTextureReadWrite.Linear
    );
    var previous = RenderTexture.active;
    RenderTexture.active = target;

    GL.Clear(true, true, Color.clear);
    GL.PushMatrix();
    GL.LoadIdentity();
    GL.LoadProjectionMatrix(Matrix4x4.Ortho(
      bounds.min.x - padding, bounds.max.x + padding,
      bounds.min.y - padding, bounds.max.y + padding,
      -1f, 1f
    ));
    material.SetPass(0);
    Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
    GL.PopMatrix();

    var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
    readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
    readback.Apply();

    RenderTexture.active = previous;
    RenderTexture.ReleaseTemporary(target);

    var mask = readback.GetPixels32().Select(it => it.a > 127).ToArray();

    Object.DestroyImmediate(readback);
    Object.DestroyImmediate(material);
    Object.DestroyImmediate(mesh);
    return mask;
  }

  /// Positive inside the shape, negative outside, in pixels.
  private static float[] SignedDistance(bool[] mask, int width, int height)
  {
    var toInside = Propagated(mask, width, height, true);
    var toOutside = Propagated(mask, width, height, false);
    return Enumerable.Range(0, mask.Length)
      .Select(i => toOutside[i].magnitude - toInside[i].magnitude)
      .ToArray();
  }

  private static Vector2[] Propagated(bool[] mask, int width, int height, bool seed)
  {
    var far = new Vector2(width + height, width + height);
    var grid = mask.Select(it => it == seed ? Vector2.zero : far).ToArray();

    for (var y = 0; y < height; y++)
    {
      for (var x = 0; x < width; x++)
      {
        Relax(grid, width, height, x, y, -1, 0);
        Relax(grid, width, height, x, y, 0, -1);
        Relax(grid, width, height, x, y, -1, -1);
        Relax(grid, width, height, x, y, 1, -1);
      }
      for (var x = width - 1; x >= 0; x--)
      {
        Relax(grid, width, height, x, y, 1, 0);
      }
    }

    for (var y = height - 1; y >= 0; y--)
    {
      for (var x = width - 1; x >= 0; x--)
      {
        Relax(grid, width, height, x, y, 1, 0);
        Relax(grid, width, height, x, y, 0, 1);
        Relax(grid, width, height, x, y, -1, 1);
        Relax(grid, width, height, x, y, 1, 1);
      }
      for (var x = 0; x < width; x++)
      {
        Relax(grid, width, height, x, y, -1, 0);
      }
    }

    return grid;
  }

  private static void Relax(
    Vector2[] grid,
    int width,
    int height,
    int x,
    int y,
    int dx,
    int dy
  )
  {
    var nx = x + dx;
    var ny = y + dy;
    if (nx < 0 || width <= nx || ny < 0 || height <= ny)
    {
      return;
    }

    var candidate = grid[ny * width + nx] + new Vector2(dx, dy);
    var index = y * width + x;
    if (candidate.sqrMagnitude < grid[index].sqrMagnitude)
    {
      grid[index] = candidate;
    }
  }

  private static Color[] Downsample(
    float[] distance,
    int width,
    int height,
    float spread
  )
  {
    var sourceWidth = width * Supersample;
    var pixels = new Color[width * height];

    for (var y = 0; y < height; y++)
    {
      for (var x = 0; x < width; x++)
      {
        var total = 0f;
        for (var sy = 0; sy < Supersample; sy++)
        {
          for (var sx = 0; sx < Supersample; sx++)
          {
            total += distance[(y * Supersample + sy) * sourceWidth + x * Supersample + sx];
          }
        }

        var average = total / (Supersample * Supersample);
        var encoded = Mathf.Clamp01(0.5f + average / (spread * 2f));
        pixels[y * width + x] = new Color(encoded, encoded, encoded, 1f);
      }
    }

    return pixels;
  }

  private static void ApplyImportSettings(string path, float pixelsPerUnit)
  {
    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
    {
      return;
    }

    var settings = new TextureImporterSettings();
    importer.ReadTextureSettings(settings);
    settings.textureType = TextureImporterType.Sprite;
    settings.spriteMode = (int)SpriteImportMode.Single;
    settings.spriteMeshType = SpriteMeshType.FullRect;
    settings.spriteAlignment = (int)SpriteAlignment.Center;
    settings.spritePixelsPerUnit = pixelsPerUnit;
    settings.sRGBTexture = false;
    settings.alphaIsTransparency = false;
    settings.mipmapEnabled = false;
    settings.wrapMode = TextureWrapMode.Clamp;
    settings.filterMode = FilterMode.Bilinear;
    importer.SetTextureSettings(settings);
    importer.textureCompression = TextureImporterCompression.Uncompressed;
    importer.SaveAndReimport();
  }
}
