using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal static class Main
{
  private static async UniTask MainAsync(
    Transform root,
    AssetRegistry assets,
    CancellationToken ct
  )
  {
    var preferences = Preferences.Of(assets.DefaultPreferences);
    root.CreateChild(
      assets.ApplicationEnterTransition,
      out var appEnterTransition
    );
    var titleEnterFadeIn = appEnterTransition.CoverInstant();
    while (true)
    {
      ct.ThrowIfCancellationRequested();
      var (stage, gameFadeIn) =
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
    // ReSharper disable once FunctionNeverReturns
  }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static async void Boot()
  {
    try
    {
      var assetRegistry =
        (AssetRegistry)await Resources.LoadAsync("AssetRegistry");
      var rootObject = new GameObject("Root");
      await MainAsync(
        rootObject.transform,
        assetRegistry,
        ct: Application.exitCancellationToken
      ).SuppressCancellationThrow();
    }
    catch (Exception e)
    {
      Debug.LogError($"An exception was not handled: {e}");
    }
    finally
    {
#if UNITY_EDITOR
      UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
  }
}
