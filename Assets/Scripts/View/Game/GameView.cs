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
      stageCamera.Init(bounds);

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
            pref: pref,
            ct: cts.Token
          ).Forget();
          var returnTask = instantiated.WaitForReturnToTitleActionAsync(parent, gameInput, cts.Token);
          var clearTask =
            instantiated.WaitForStageCompletionAsync(
              parent,
              gameInput.MovePlayerForward,
              gameInput.MovePlayerRight,
              gameInput.MovePlayerBackward,
              gameInput.MovePlayerLeft,
              stage,
              stageCamera,
              (id) => idToObj[id],
              cts.Token
            );
          (_, var anim) =
            await UniTask.WhenAny<Func<CancellationToken, UniTask<Func<CancellationToken, UniTask>>>>(
              returnTask,
              clearTask
            );
          return await anim(ct);
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
          spawned.transform.SetLocalPositionAndRotation(
            localPosition: new(pos.X, pos.Y, pos.Z),
            localRotation: Quaternion.Euler(0f, 180f, 0f)
          );
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
    CreateFloor(parent, (preset.Size.x, preset.Size.z), preset.Floor0Prefab, preset.Floor1Prefab);
    return (idToObj, stage);
  }

  private static void CreateFloor(
    Transform parent,
    (int x, int z) size,
    GameObject floor0Prefab,
    GameObject floor1Prefab
  )
  {
    var pivot = parent.CreateChild("FloorPivot");
    var floorPrefabs = new[] { floor0Prefab, floor1Prefab };
    foreach (var x in Enumerable.Range(0, size.x))
    {
      foreach (var z in Enumerable.Range(0, size.z))
      {
        var floorPrefab = floorPrefabs[(x + (z % floorPrefabs.Length)) % floorPrefabs.Length];
        var created = pivot.CreateChild(floorPrefab);
        created.GameObject.transform.localPosition = new(x, 0f, z);
      }
    }
  }

  private async UniTask OrbitCameraForInputAsync(
    InputAction mouseDelta,
    StageCamera camera,
    Preferences pref,
    CancellationToken ct
  )
  {
    void OnPerform(InputAction.CallbackContext ctx)
    {
      var delta = ctx.ReadValue<Vector2>();
      camera.OrbitAsync(
        ct,
        deltaYaw: delta.x * pref.Current.CameraYawDegreesPerPixel,
        deltaPitch: delta.y * pref.Current.CameraPitchDegreesPerPixel
      ).Forget();
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

  private async UniTask<Func<CancellationToken, UniTask<Func<CancellationToken, UniTask>>>> WaitForStageCompletionAsync(
    Transform transitionParent,
    InputAction moveForward,
    InputAction moveRight,
    InputAction moveBackward,
    InputAction moveLeft,
    GameStage stage,
    StageCamera camera,
    Func<GameStage.EntityId, StageObject> idToObj,
    CancellationToken ct
  )
  {
    while (true)
    {
      ct.ThrowIfCancellationRequested();
      var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      var moveTask =
        MovePlayerForInputAsync(moveForward, moveRight, moveBackward, moveLeft, stage, camera, cts.Token);
      var undoTask = UndoMovementForInputAsync(stage, cts.Token);
      var redoTask = RedoMovementForInputAsync(stage, cts.Token);
      try
      {
        (_, var animate) = await UniTask.WhenAny<
          Func<Func<GameStage.EntityId, StageObject>, CancellationToken, UniTask>
        >(moveTask, undoTask, redoTask);
        await animate(idToObj, ct);
        if (stage.IsCleared)
        {
          transitionParent.CreateChild(returnTransitionPrefab, out var transition);
          return async (ct) => await transition.CoverAsync(ct);
        }
      }
      finally
      {
        cts.Cancel();
      }
    }
  }

  private async UniTask<
    Func<
      Func<GameStage.EntityId, StageObject>,
      CancellationToken,
      UniTask
    >
  > MovePlayerForInputAsync(
    InputAction moveForward,
    InputAction moveRight,
    InputAction moveBackward,
    InputAction moveLeft,
    GameStage stage,
    StageCamera camera,
    CancellationToken ct
  )
  {
    var worldDirection =
      await WaitForPlayerMoveInputAsync(moveForward, moveRight, moveBackward, moveLeft, ct);
    var movements = stage.MovePlayers(camera.CameraLocalInput(worldDirection));
    undoButton.interactable = stage.CanUndo;
    redoButton.interactable = stage.CanRedo;

    return async (idToObj, ctx) =>
    {
      undoButton.interactable = false;
      redoButton.interactable = false;
      foreach (var turn in movements)
      {
        await turn.Select(mv =>
        {
          (var x, var y, var z) = mv.To;
          return idToObj(mv.Who).MoveTo(new(x, y, z), ct);
        });
      }
      undoButton.interactable = stage.CanUndo;
      redoButton.interactable = stage.CanRedo;
    };
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

  private async UniTask<
    Func<
      Func<GameStage.EntityId, StageObject>,
      CancellationToken,
      UniTask
    >
  > UndoMovementForInputAsync(
    GameStage stage,
    CancellationToken ct
  )
  {
    await undoButton.OnClickAsync(ct);
    var undone = stage.Undo();
    undoButton.interactable = stage.CanUndo;
    redoButton.interactable = stage.CanRedo;
    return async (idToObj, ctx) =>
    {
      undoButton.interactable = false;
      redoButton.interactable = false;
      foreach (var turn in undone.Reverse())
      {
        await turn.Select(mv =>
        {
          (var x, var y, var z) = mv.From;
          return idToObj(mv.Who).RewindTo(new(x, y, z), ct);
        });
      }
      undoButton.interactable = stage.CanUndo;
      redoButton.interactable = stage.CanRedo;
    };
  }

  private async UniTask<
    Func<
      Func<GameStage.EntityId, StageObject>,
      CancellationToken,
      UniTask
    >
  > RedoMovementForInputAsync(
    GameStage stage,
    CancellationToken ct
  )
  {
    await redoButton.OnClickAsync(ct);
    var redone = stage.Redo();
    undoButton.interactable = stage.CanUndo;
    redoButton.interactable = stage.CanRedo;
    return async (idToObj, ctx) =>
    {
      undoButton.interactable = false;
      redoButton.interactable = false;
      foreach (var turn in redone)
      {
        await turn.Select(mv =>
        {
          (var x, var y, var z) = mv.To;
          return idToObj(mv.Who).MoveTo(new(x, y, z), ct);
        });
      }
      undoButton.interactable = stage.CanUndo;
      redoButton.interactable = stage.CanRedo;
    };
  }

  private async UniTask<Func<CancellationToken, UniTask<Func<CancellationToken, UniTask>>>> WaitForReturnToTitleActionAsync(
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
          return async (ct) => await transition.CoverAsync(ct);
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
