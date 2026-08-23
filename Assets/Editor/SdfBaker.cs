using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

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

    var mask = RasterizeMask(sprite, bounds, padding, width * Supersample, height * Supersample);
    var distance = SignedDistance(mask, width * Supersample, height * Supersample);
    var pixels = Downsample(distance, width, height, spread);

    var covered = mask.Count(it => it);
    if (covered == 0)
    {
      Debug.LogError(
        $"{AssetDatabase.GetAssetPath(sprite)}: the sprite mesh covered no pixels " +
        $"({sprite.vertices.Length} vertices, {sprite.triangles.Length / 3} triangles). " +
        "Nothing was baked."
      );
      return;
    }

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

  /// Scanline rasterization on the CPU, so the bake does not depend on shader
  /// compilation, render textures or the editor being outside play mode.
  private static bool[] RasterizeMask(
    Sprite sprite,
    Bounds bounds,
    float padding,
    int width,
    int height
  )
  {
    var origin = new Vector2(bounds.min.x - padding, bounds.min.y - padding);
    var scale = width / (bounds.size.x + padding * 2f);

    var vertices = sprite.vertices
      .Select(it => (it - origin) * scale)
      .ToArray();
    var triangles = sprite.triangles;
    var mask = new bool[width * height];

    for (var i = 0; i + 2 < triangles.Length; i += 3)
    {
      FillTriangle(
        mask,
        width,
        height,
        vertices[triangles[i]],
        vertices[triangles[i + 1]],
        vertices[triangles[i + 2]]
      );
    }

    return mask;
  }

  private static void FillTriangle(
    bool[] mask,
    int width,
    int height,
    Vector2 a,
    Vector2 b,
    Vector2 c
  )
  {
    var left = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)));
    var right = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x)));
    var bottom = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)));
    var top = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y)));

    for (var y = bottom; y <= top; y++)
    {
      for (var x = left; x <= right; x++)
      {
        var point = new Vector2(x + 0.5f, y + 0.5f);
        var ab = Edge(a, b, point);
        var bc = Edge(b, c, point);
        var ca = Edge(c, a, point);
        // Accept either winding order: the tessellator emits both.
        if ((ab >= 0f && bc >= 0f && ca >= 0f) || (ab <= 0f && bc <= 0f && ca <= 0f))
        {
          mask[y * width + x] = true;
        }
      }
    }
  }

  private static float Edge(Vector2 from, Vector2 to, Vector2 point) =>
    (to.x - from.x) * (point.y - from.y) - (to.y - from.y) * (point.x - from.x);

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
    settings.npotScale = TextureImporterNPOTScale.None;
    settings.wrapMode = TextureWrapMode.Clamp;
    settings.filterMode = FilterMode.Bilinear;
    importer.SetTextureSettings(settings);
    importer.textureCompression = TextureImporterCompression.Uncompressed;
    importer.SaveAndReimport();
  }
}
