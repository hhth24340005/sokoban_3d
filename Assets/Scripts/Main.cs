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
    using (systemRoot.gameObject.ChildOf("Sun").With<Light>(out var sun))
    {
      sun.type = LightType.Directional;
      sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

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

        if (stage < 0 || assets.GameStagePresets.Count <= stage)
        {
          Debug.LogError($"No game stage preset at index {stage}.");
          continue;
        }

        await Game(
          assets.GameView,
          assets.GameStagePresets[stage],
          systemRoot.gameObject,
          systemRoot.MainCamera,
          systemRoot.UIRoot.gameObject,
          ct
        );
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
    GameStagePreset stagePreset,
    GameObject stageParent,
    Camera camera,
    GameObject uiRoot,
    CancellationToken ct
  )
  {
    PlaceCamera(camera);
    using (var stage = GameStage.Create(stagePreset, stageParent))
    using (var input = new KeyboardDirectionInput())
    using (uiRoot.ChildOf(gameViewPrefab, out var gameView))
    {
      var controller = new StageController(stage);
      using var gameplayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      var stepLoop = StepLoop(controller, input, gameView, gameplayCts.Token).Preserve();
      var debugClear = gameView.WaitForGameClearActionAsync(gameplayCts.Token).Preserve();
      await UniTask.WhenAny(stepLoop, debugClear);
      gameplayCts.Cancel();
      await UniTask.WhenAll(
        stepLoop.SuppressCancellationThrow(),
        debugClear.SuppressCancellationThrow()
      );
    }
  }

  private static async UniTask StepLoop(
    StageController controller,
    KeyboardDirectionInput input,
    GameView gameView,
    CancellationToken ct
  )
  {
    gameView.SetHistoryState(controller.CanUndo, controller.CanRedo);
    while (!controller.IsCleared())
    {
      switch (await NextAction(input, gameView, ct))
      {
        case GameAction.Move move:
          await controller.Step(move.Direction, ct);
          break;
        case GameAction.Undo:
          await controller.Undo(ct);
          break;
        case GameAction.Redo:
          await controller.Redo(ct);
          break;
      }
      gameView.SetHistoryState(controller.CanUndo, controller.CanRedo);
    }
  }

  private static async UniTask<GameAction> NextAction(
    KeyboardDirectionInput input,
    GameView gameView,
    CancellationToken ct
  )
  {
    var move = MoveAction(input, ct);
    var undo = UndoAction(gameView, ct);
    var redo = RedoAction(gameView, ct);
    (_, var action) = await UniTask.WhenAny<GameAction>(move, undo, redo);
    return action;
  }

  private static async UniTask<GameAction> MoveAction(
    KeyboardDirectionInput input,
    CancellationToken ct
  )
  {
    var direction = await input.NextStepDirection(ct);
    return new GameAction.Move(direction);
  }

  private static async UniTask<GameAction> UndoAction(GameView gameView, CancellationToken ct)
  {
    await gameView.WaitForUndoActionAsync(ct);
    return new GameAction.Undo();
  }

  private static async UniTask<GameAction> RedoAction(GameView gameView, CancellationToken ct)
  {
    await gameView.WaitForRedoActionAsync(ct);
    return new GameAction.Redo();
  }

  private static void PlaceCamera(Camera camera)
  {
    camera.transform.position = new Vector3(2f, 4f, -5f);
    camera.transform.LookAt(new Vector3(2f, 0f, 2f));
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
