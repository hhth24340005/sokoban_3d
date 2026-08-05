using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class StageObjectViewTranslationTests
{
  [Test]
  public void AllStageObjectViewImplementationsAreTranslated()
  {
    var preset = ScriptableObject.CreateInstance<GameStagePreset>();
    var stage = GameStage.Create(preset);
    var probe = new GameObject(nameof(StageObjectViewTranslationTests));

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
          () => stage.ToModel(view),
          $"GameStage does not translate {viewType} into a model-side IStageObject."
        );
      }
    }
    finally
    {
      Object.DestroyImmediate(probe);
      Object.DestroyImmediate(preset);
    }
  }
}
