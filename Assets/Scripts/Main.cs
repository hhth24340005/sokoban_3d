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
    while (true)
    {
      ct.ThrowIfCancellationRequested();
      (var stage, var gameFadeIn) =
        await assets.TitleView.PlayAsync(
          parent: root,
          fadeIn: titleEnterFadeIn,
          pref: preferences,
          ct: ct
        );
      titleEnterFadeIn =
        await assets.GameView.PlayAsync(
          parent: root,
          fadeIn: gameFadeIn,
          preset: stage,
          pref: preferences,
          ct: ct
        );
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
#if UNITY_EDITOR
      UnityEditor.EditorApplication.isPlaying = false;
#else
      Application.Quit();
#endif
    }
  }
}
