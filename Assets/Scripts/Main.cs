using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

static class Main
{
  private static async UniTask MainAsync(
    GameObject rootObject,
    AssetRegistry assets,
    CancellationToken ct
  )
  {
    using (rootObject.ChildOf(assets.SystemRoot, out var systemRoot))
    {
      while (true)
      {
        if (
          await Title(
            assets.TitleView,
            systemRoot.UIRoot.gameObject,
            ct
          ) is not int stage
        )
        {
          return;
        }

        // test
        Debug.Log(stage);
        await Game(assets.GameView, systemRoot.UIRoot.gameObject, ct);
      }
    }
  }

  private static async UniTask<int?> Title(
    TitleView titleViewPrefab,
    GameObject uiRoot,
    CancellationToken ct
  )
  {
    using (uiRoot.ChildOf(titleViewPrefab, out var titleView))
    {
      var action = await titleView.WaitForActionAsync(ct);
      return action switch
      {
        TitleView.Result.Start s => s.NextStage,
        _ => null
      };
    }
  }

  private static async UniTask Game(
    GameView gameViewPrefab,
    GameObject uiRoot,
    CancellationToken ct
  )
  {
    using (uiRoot.ChildOf(gameViewPrefab, out var gameView))
    {
      await gameView.WaitForGameClearActionAsync(ct);
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
    await MainAsync(
      rootObject,
      assetRegistry,
      ct: Application.exitCancellationToken
    ).SuppressCancellationThrow();
    Application.Quit();
#if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying = false;
#endif
  }
}
