using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

static class Main
{
  private static async UniTask MainAsync(
    GameObject rootObject,
    CancellationToken ct
  )
  {
    // test
    await UniTask.Delay(3000, cancellationToken: ct);
  }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static async void Boot()
  {
    var originalScene = SceneManager.GetActiveScene();
    var rootSceneName = $"Root-{Guid.NewGuid()}";
    var rootScene = SceneManager.CreateScene(rootSceneName);
    SceneManager.SetActiveScene(rootScene);
    await SceneManager.UnloadSceneAsync(originalScene);

    var rootObject = new GameObject("Root");
    await MainAsync(
      rootObject,
      ct: Application.exitCancellationToken
    ).SuppressCancellationThrow();

    Application.Quit();
#if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying = false;
#endif
  }
}
