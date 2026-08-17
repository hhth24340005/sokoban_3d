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
          return await instantiated.WaitForReturnToTitleActionAsync(parent, gameInput, cts.Token);
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
    IReadOnlyDictionary<GameStage.EntityId, GameObject> idToObj,
    GameStage stage
  ) CreateStage(Transform parent, GameStagePreset preset)
  {
    var id = 0;
    var prefabToPos = preset.GetGameObjectPositions();
    var objToPos =
      prefabToPos.SelectMany(kv =>
      {
        (var prefab, var positions) = kv;
        return positions.Select(pos =>
        {
          var spawned = parent.CreateChild(prefab).GameObject;
          spawned.transform.localPosition = new(pos.X, pos.Y, pos.Z);
          return (spawned, pos);
        });
      }).ToImmutableDictionary(it => it.spawned, it => it.pos);
    var idToObj = objToPos.ToImmutableDictionary((_) => new GameStage.EntityId(id++), it => it.Key);
    var idToPos =
      idToObj.Join(
        inner: objToPos,
        outerKeySelector: idToPrefab => idToPrefab.Value,
        innerKeySelector: prefabToPos => prefabToPos.Key,
        resultSelector:
          (idToPrefab, prefabToPos) => new { idToPrefab.Key, prefabToPos.Value }
      ).ToDictionary(it => it.Key, it => it.Value);
    var size = preset.Size;
    var stage = new GameStage((size.x, size.y, size.z), idToPos);
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
