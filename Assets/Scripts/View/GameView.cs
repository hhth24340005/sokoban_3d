using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class GameView : MonoBehaviour
{
  [SerializeField]
  private PauseView pauseView;

  [SerializeField]
  private TransitionView returnTransitionPrefab;

  [SerializeField]
  private Button undoButton;

  [SerializeField]
  private Button redoButton;

  public async UniTask<Func<CancellationToken, UniTask>> PlayAsync(
    Transform parent,
    Func<CancellationToken, UniTask> fadeIn,
    GameStagePreset preset,
    Preferences pref,
    CancellationToken ct
  )
  {
    using (parent.CreateChild(this, out var instantiated, copyIfExisting: false))
    using (instantiated.gameObject.CreateChild(preset.StageCameraPrefab, out var stageCamera))
    {
      (var idToObj, var stage) = CreateStage(instantiated.transform, preset);
      var bounds = new Bounds(center: preset.Center, size: preset.Size);
      stageCamera.Orbit(bounds);

      using var inputAction = new PlayerInputActions();
      var gameInput = inputAction.Game;
      try
      {
        gameInput.Enable();
        await fadeIn(ct);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
          instantiated.OrbitCameraForInputAsync(
            mouseDelta: gameInput.MoveCamera,
            camera: stageCamera,
            bounds: bounds,
            pref: pref,
            ct: cts.Token
          ).Forget();
          var returnTask = instantiated.WaitForReturnToTitleActionAsync(parent, gameInput, cts.Token);
          var clearTask =
            instantiated.WaitForStageCompletionAsync(
              gameInput.MovePlayerForward,
              gameInput.MovePlayerRight,
              gameInput.MovePlayerBackward,
              gameInput.MovePlayerLeft,
              stage,
              (id) => idToObj[id],
              cts.Token
            );
          (_, var ret) = await UniTask.WhenAny<Func<CancellationToken, UniTask>>(returnTask, clearTask);
          return ret;
        }
        finally
        {
          cts.Cancel();
        }
      }
      finally
      {
        gameInput.Disable();
      }
    }
  }

  private static (
    IReadOnlyDictionary<GameStage.EntityId, StageObject> idToObj,
    GameStage stage
  ) CreateStage(Transform parent, GameStagePreset preset)
  {
    var id = 0;
    var prefabToPos = preset.GetGameObjectPositions();
    var typeToRules = new Dictionary<GameStage.TypeId, IReadOnlyCollection<GameStage.Rule>>();
    var objToData =
      prefabToPos.SelectMany((kv, type) =>
      {
        (var prefab, var positions) = kv;
        var typeId = new GameStage.TypeId(type);
        typeToRules.Add(typeId, prefab.Rules);

        return positions.Select(pos =>
        {
          parent.CreateChild(prefab, out var spawned);
          spawned.transform.localPosition = new(pos.X, pos.Y, pos.Z);
          var data = (typeId, pos);
          return (spawned, data);
        });
      }).ToImmutableDictionary(it => it.spawned, it => it.data);
    var idToObj = objToData.ToImmutableDictionary((_) => new GameStage.EntityId(id++), it => it.Key);
    var idToData =
      idToObj.Join(
        inner: objToData,
        outerKeySelector: idToPrefab => idToPrefab.Value,
        innerKeySelector: prefabToPos => prefabToPos.Key,
        resultSelector:
          (idToPrefab, prefabToPos) => new { idToPrefab.Key, prefabToPos.Value }
      ).ToImmutableDictionary(it => it.Key, it => it.Value);
    var size = preset.Size;
    var stage =
      new GameStage(
        (size.x, size.y, size.z),
        idToData,
        typeToRules.ToImmutableDictionary()
      );
    return (idToObj, stage);
  }

  private async UniTask OrbitCameraForInputAsync(
    InputAction mouseDelta,
    StageCamera camera,
    Bounds bounds,
    Preferences pref,
    CancellationToken ct
  )
  {
    void OnPerform(InputAction.CallbackContext ctx)
    {
      var delta = ctx.ReadValue<Vector2>();
      camera.Orbit(
        bounds,
        deltaYaw: delta.x * pref.Current.CameraYawDegreesPerPixel,
        deltaPitch: delta.y * pref.Current.CameraPitchDegreesPerPixel
      );
    }
    try
    {
      mouseDelta.performed += OnPerform;
      await ct.WaitUntilCanceled();
    }
    finally
    {
      mouseDelta.performed -= OnPerform;
    }
  }

  private async UniTask<Func<CancellationToken, UniTask>> WaitForStageCompletionAsync(
    InputAction moveForward,
    InputAction moveRight,
    InputAction moveBackward,
    InputAction moveLeft,
    GameStage stage,
    Func<GameStage.EntityId, StageObject> idToObj,
    CancellationToken ct
  )
  {
    while (true)
    {
      ct.ThrowIfCancellationRequested();
      {
        var movements =
          await MovePlayerForInputAsync(moveForward, moveRight, moveBackward, moveLeft, stage, ct);
        var animations = movements.Select(mv =>
        {
          (var x, var y, var z) = mv.To;
          return idToObj(mv.Who).MoveTo(new(x, y, z), ct);
        });
        await UniTask.WhenAll(animations);
      }
      {
        var falls = stage.FallGravitationals();
        var animations = falls.Select(mv =>
        {
          (var x, var y, var z) = mv.To;
          return idToObj(mv.Who).MoveTo(new(x, y, z), ct);
        });
        await UniTask.WhenAll(animations);
      }
    }
  }

  private async UniTask<IEnumerable<GameStage.Movement>> MovePlayerForInputAsync(
    InputAction moveForward,
    InputAction moveRight,
    InputAction moveBackward,
    InputAction moveLeft,
    GameStage stage,
    CancellationToken ct
  )
  {
    var direction =
      await WaitForPlayerMoveInputAsync(moveForward, moveRight, moveBackward, moveLeft, ct);
    return stage.MovePlayers(direction);
  }

  private async UniTask<GameStage.Direction> WaitForPlayerMoveInputAsync(
    InputAction moveForward,
    InputAction moveRight,
    InputAction moveBackward,
    InputAction moveLeft,
    CancellationToken ct
  )
  {
    var tcs = new UniTaskCompletionSource<GameStage.Direction>();
    using var _ = ct.Register(() => tcs.TrySetCanceled());
    Action<InputAction.CallbackContext> CallbackOf(GameStage.Direction direction) =>
      (ctx) => tcs.TrySetResult(direction);
    var forwardCb = CallbackOf(GameStage.Direction.PlusZ);
    var rightCb = CallbackOf(GameStage.Direction.PlusX);
    var backwardCb = CallbackOf(GameStage.Direction.MinusZ);
    var leftCb = CallbackOf(GameStage.Direction.MinusX);
    try
    {
      moveForward.performed += forwardCb;
      moveRight.performed += rightCb;
      moveBackward.performed += backwardCb;
      moveLeft.performed += leftCb;
      return await tcs.Task;
    }
    finally
    {
      moveForward.performed -= forwardCb;
      moveRight.performed -= rightCb;
      moveBackward.performed -= backwardCb;
      moveLeft.performed -= leftCb;
    }
  }

  private async UniTask<Func<CancellationToken, UniTask>> WaitForReturnToTitleActionAsync(
    Transform parent,
    PlayerInputActions.GameActions inputAction,
    CancellationToken ct
  )
  {
    while (true)
    {
      ct.ThrowIfCancellationRequested();
      await WaitForPauseActionAsync(inputAction, ct);
      var pauseResult = await pauseView.ShowAndWaitForAction(inputAction, ct);
      switch (pauseResult)
      {
        case PauseView.Result.Resume:
          continue;
        case PauseView.Result.ReturnToTitle:
          parent.CreateChild(returnTransitionPrefab, out var transition);
          return await transition.CoverAsync(ct);
        default:
          throw new Exception($"Unknown {typeof(PauseView.Result)} type >.<");
      }
    }
  }

  private static async UniTask WaitForPauseActionAsync(
    PlayerInputActions.GameActions inputAction,
    CancellationToken ct
  )
  {
    var tcs = new UniTaskCompletionSource();
    using var _ = ct.Register(() => tcs.TrySetCanceled());
    void OnPerform(InputAction.CallbackContext ctx) => tcs.TrySetResult();
    inputAction.Pause.performed += OnPerform;
    try
    {
      await tcs.Task;
    }
    finally
    {
      inputAction.Pause.performed -= OnPerform;
    }
  }
}
