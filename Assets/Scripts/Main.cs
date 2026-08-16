using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

static class Main
{
  private static async UniTask MainAsync(
    Transform root,
    AssetRegistry assets,
    CancellationToken ct
  )
  {
    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    RenderSettings.ambientLight = new Color(0.7f, 0.7f, 0.7f);

    var preferences = Preferences.Of(assets.DefaultPreferences);
    root.CreateChild(assets.ApplicationEnterTransition, out var appEnterTransition);
    var titleEnterFadeIn = appEnterTransition.Cover();
    while (!ct.IsCancellationRequested)
    {
      var titleResult =
        await assets.TitleView.PlayAsync(
          parent: root,
          fadeIn: titleEnterFadeIn,
          pref: preferences,
          ct: ct
        );
      if (titleResult is not TitleView.Result.Start startResult)
      {
        switch (titleResult)
        {
          case TitleView.Result.QuitGame:
            return;
          default:
            throw new Exception($"Unknown {nameof(TitleView.Result)} type >.<");
        }
      }
      await assets.GameView.PlayAsync(
        parent: root,
        preset: startResult.Stage,
        ct: ct
      );
      titleEnterFadeIn = (ct) => UniTask.CompletedTask;

    }
  }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static async void Boot()
  {
    var originalScene = SceneManager.GetActiveScene();
    var rootSceneName = $"Root-{Guid.NewGuid()}";
    var rootScene = SceneManager.CreateScene(rootSceneName);
    SceneManager.SetActiveScene(rootScene);
    await SceneManager.UnloadSceneAsync(originalScene);

    var assetRegistry = (AssetRegistry)await Resources.LoadAsync("AssetRegistry");
    var rootObject = new GameObject("Root");
    try
    {
      await MainAsync(
        rootObject.transform,
        assetRegistry,
        ct: Application.exitCancellationToken
      ).SuppressCancellationThrow();
    }
    finally
    {
      Application.Quit();
#if UNITY_EDITOR
      UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
  }
}
