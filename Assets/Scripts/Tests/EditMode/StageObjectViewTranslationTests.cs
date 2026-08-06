using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class StageObjectViewTranslationTests
{
  [Test]
  public void AllStageObjectViewImplementationsAreTranslated()
  {
    var preset = ScriptableObject.CreateInstance<GameStagePreset>();
    var host = new GameObject(nameof(StageObjectViewTranslationTests));
    var stage = GameStage.Create(preset, host);
    var probe = new GameObject($"{nameof(StageObjectViewTranslationTests)}.Probe");

    try
    {
      var viewTypes = typeof(GameStage).Assembly.GetTypes()
        .Where(t => typeof(IStageObjectView).IsAssignableFrom(t))
        .Where(t => !t.IsAbstract && !t.IsInterface)
        .Where(t => typeof(Component).IsAssignableFrom(t));

      foreach (var viewType in viewTypes)
      {
        var view = (IStageObjectView)probe.AddComponent(viewType);
        Assert.DoesNotThrow(
          () => stage.ToModel(view, Vector3Int.zero),
          $"GameStage does not translate {viewType} into a model-side IStageObject."
        );
      }
    }
    finally
    {
      // Destroying [host] also destroys the board root GameStage.Create nested
      // under it. DestroyImmediate is required in edit mode (Dispose would call
      // the edit-mode-illegal Object.Destroy).
      Object.DestroyImmediate(probe);
      Object.DestroyImmediate(host);
      Object.DestroyImmediate(preset);
    }
  }
}
