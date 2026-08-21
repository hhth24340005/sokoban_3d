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

  [SerializeField]
  private AudioSource musicSource;

  [SerializeField]
  private AudioSource seSource;

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
      var (idToObj, stage) = CreateStage(instantiated.transform, preset);
      var players = idToObj.Values.OfType<PlayerStageObject>().ToImmutableList();
      var bounds = new Bounds(center: preset.Center, size: preset.Size);
      stageCamera.Init(bounds);

      using var inputAction = new PlayerInputActions();
      var gameInput = inputAction.Game;
      try
      {
        await UniTask.Delay(250, cancellationToken: ct);
        instantiated.musicSource.clip = preset.Music;
        instantiated.musicSource.Play();
        await UniTask.Delay(750, cancellationToken: ct);
        await fadeIn(ct);
        gameInput.Enable();

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
              players,
              cts.Token
            );
          var (_, anim) =
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
        var (prefab, positions) = kv;
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
        innerKeySelector: prefabToPos1 => prefabToPos1.Key,
        resultSelector:
          (idToPrefab, prefabToPos1) => new { idToPrefab.Key, prefabToPos1.Value }
      ).ToImmutableDictionary(it => it.Key, it => it.Value);
    var size = preset.Size;
    var stage =
      new GameStage(
        (size.x, size.y, size.z),
        idToData,
        typeToRules.ToImmutableDictionary()
      );
    parent.CreateChild(preset.GroundPrefab, out var ground);
    ground.Build(preset.Size);
    return (idToObj, stage);
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
    IReadOnlyCollection<PlayerStageObject> players,
    CancellationToken ct
  )
  {
    var facing = new PlayerFacing();
    while (true)
    {
      ct.ThrowIfCancellationRequested();
      var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      var moveTask =
        MovePlayerForInputAsync(
          moveForward, moveRight, moveBackward, moveLeft, stage, camera, facing, players, cts.Token);
      var undoTask = UndoMovementForInputAsync(stage, facing, cts.Token);
      var redoTask = RedoMovementForInputAsync(stage, facing, cts.Token);
      Func<Func<GameStage.EntityId, StageObject>, CancellationToken, UniTask> animate;
      try
      {
        (_, animate) = await UniTask.WhenAny<
          Func<Func<GameStage.EntityId, StageObject>, CancellationToken, UniTask>
        >(moveTask, undoTask, redoTask);
      }
      finally
      {
        cts.Cancel();
      }
      await animate(idToObj, ct);
      if (stage.IsCleared)
      {
        transitionParent.CreateChild(returnTransitionPrefab, out var transition);
        return async (ct1) => await transition.CoverAsync(ct1);
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
    PlayerFacing facing,
    IReadOnlyCollection<PlayerStageObject> players,
    CancellationToken ct
  )
  {
    var worldDirection =
      await WaitForPlayerMoveInputAsync(moveForward, moveRight, moveBackward, moveLeft, ct);
    var direction = camera.CameraLocalInput(worldDirection);
    var movements = stage.MovePlayers(direction);
    var bumped = movements.Count <= 0 && facing.Current == direction;
    if (movements.Count > 0)
    {
      facing.Advance(direction);
    }
    else
    {
      facing.Turn(direction);
    }
    undoButton.interactable = stage.CanUndo;
    redoButton.interactable = stage.CanRedo;

    return async (idToObj, ct1) =>
    {
      undoButton.interactable = false;
      redoButton.interactable = false;
      if (movements.Count > 0)
      {
        await movements
          .Select(entry =>
            AnimatePathAsync(idToObj(entry.Key), entry.Value, rewind: false, direction, ct1))
          .ToImmutableList();
      }
      else if (bumped)
      {
        await BumpPlayersAsync(players, direction, ct1);
      }
      else
      {
        await TurnPlayersAsync(players, direction, ct1);
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
    await using var _ = ct.Register(() => tcs.TrySetCanceled());
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

    Action<InputAction.CallbackContext> CallbackOf(GameStage.Direction direction) =>
      _ => tcs.TrySetResult(direction);
  }

  private async UniTask<
    Func<
      Func<GameStage.EntityId, StageObject>,
      CancellationToken,
      UniTask
    >
  > UndoMovementForInputAsync(
    GameStage stage,
    PlayerFacing facing,
    CancellationToken ct
  )
  {
    await undoButton.OnClickAsync(ct);
    var undone = stage.Undo();
    if (undone.Count > 0)
    {
      facing.Undo();
    }
    var restored = facing.Current;
    undoButton.interactable = stage.CanUndo;
    redoButton.interactable = stage.CanRedo;
    return async (idToObj, ct1) =>
    {
      undoButton.interactable = false;
      redoButton.interactable = false;
      await undone
        .Select(entry =>
          AnimatePathAsync(idToObj(entry.Key), entry.Value, rewind: true, restored, ct1))
        .ToImmutableList();
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
    PlayerFacing facing,
    CancellationToken ct
  )
  {
    await redoButton.OnClickAsync(ct);
    var redone = stage.Redo();
    if (redone.Count > 0)
    {
      facing.Redo();
    }
    var restored = facing.Current;
    undoButton.interactable = stage.CanUndo;
    redoButton.interactable = stage.CanRedo;
    return async (idToObj, ct1) =>
    {
      undoButton.interactable = false;
      redoButton.interactable = false;
      await redone
        .Select(entry =>
          AnimatePathAsync(idToObj(entry.Key), entry.Value, rewind: false, restored, ct1))
        .ToImmutableList();
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
      musicSource.Pause();
      var pauseResult = await pauseView.ShowAndWaitForAction(seSource, inputAction, ct);
      switch (pauseResult)
      {
        case PauseView.Result.Resume:
          musicSource.UnPause();
          continue;
        case PauseView.Result.ReturnToTitle:
          parent.CreateChild(returnTransitionPrefab, out var transition);
          return async ct1 => await transition.CoverAsync(ct1);
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
    await using var _ = ct.Register(() => tcs.TrySetCanceled());
    inputAction.Pause.performed += OnPerform;
    try
    {
      await tcs.Task;
    }
    finally
    {
      inputAction.Pause.performed -= OnPerform;
    }
    return;

    void OnPerform(InputAction.CallbackContext ctx) => tcs.TrySetResult();
  }

  private static UniTask AnimatePathAsync(
    StageObject stageObject,
    IReadOnlyList<GameStage.Movement> path,
    bool rewind,
    GameStage.Direction facing,
    CancellationToken ct
  ) =>
    stageObject is PlayerStageObject player
      ? AnimatePlayerPathAsync(player, path, rewind, facing, ct)
      : AnimateOrderedAsync(stageObject, Ordered(path, rewind), rewind, turnAt: -1, null, ct);

  private static async UniTask AnimatePlayerPathAsync(
    PlayerStageObject player,
    IReadOnlyList<GameStage.Movement> path,
    bool rewind,
    GameStage.Direction facing,
    CancellationToken ct
  )
  {
    var ordered = Ordered(path, rewind);
    var turnAt = rewind
      ? ordered.FindLastIndex(IsHorizontal)
      : ordered.FindIndex(IsHorizontal);
    await AnimateOrderedAsync(player, ordered, rewind, turnAt, facing, ct);
    if (turnAt < 0)
    {
      await player.TurnAsync(facing, ct);
    }
  }

  private static async UniTask AnimateOrderedAsync(
    StageObject stageObject,
    ImmutableList<GameStage.Movement> ordered,
    bool rewind,
    int turnAt,
    GameStage.Direction? facing,
    CancellationToken ct
  )
  {
    for (var i = 0; i < ordered.Count; i++)
    {
      var animation = AnimateAsync(stageObject, ordered[i], rewind, ct);
      if (i == turnAt && facing is { } direction && stageObject is PlayerStageObject player)
      {
        await UniTask.WhenAll(animation, player.TurnAsync(direction, ct));
      }
      else
      {
        await animation;
      }
    }
  }

  private static ImmutableList<GameStage.Movement> Ordered(
    IReadOnlyList<GameStage.Movement> path,
    bool rewind
  ) => (rewind ? path.Reverse() : path).ToImmutableList();

  private static bool IsHorizontal(GameStage.Movement movement) =>
    movement.From.X != movement.To.X || movement.From.Z != movement.To.Z;

  private static UniTask AnimateAsync(
    StageObject stageObject,
    GameStage.Movement movement,
    bool rewind,
    CancellationToken ct
  )
  {
    var ((x0, y0, z0), (x1, y1, z1)) = movement;
    var past = new Vector3(x0, y0, z0);
    var current = new Vector3(x1, y1, z1);
    return (y1 - y0) switch
    {
      > 0 => stageObject.ClimbAsync((past, current), rewind, ct),
      < 0 => stageObject.FallAsync((past.y, current.y), rewind, ct),
      _ =>
        stageObject.WalkAsync(
          ((past.x, past.z), (current.x, current.z)),
          rewind,
          ct
        ),
    };
  }

  private static UniTask TurnPlayersAsync(
    IReadOnlyCollection<PlayerStageObject> players,
    GameStage.Direction direction,
    CancellationToken ct
  ) =>
    UniTask.WhenAll(
      players.Select(it => it.TurnAsync(direction, ct)).ToImmutableList()
    );

  private static UniTask BumpPlayersAsync(
    IReadOnlyCollection<PlayerStageObject> players,
    GameStage.Direction direction,
    CancellationToken ct
  ) =>
    UniTask.WhenAll(
      players.Select(it => it.BumpAsync(direction, ct)).ToImmutableList()
    );

  private sealed class PlayerFacing
  {
    private readonly Stack<GameStage.Direction> _moveHistory = new();
    private readonly Stack<GameStage.Direction> _undoHistory = new();

    public GameStage.Direction Current { get; private set; } = InitialDirection;

    private const GameStage.Direction InitialDirection = GameStage.Direction.MinusZ;

    public void Advance(GameStage.Direction direction)
    {
      _moveHistory.Push(direction);
      _undoHistory.Clear();
      Current = direction;
    }

    public void Turn(GameStage.Direction direction) => Current = direction;

    public void Undo()
    {
      if (_moveHistory.Count <= 0)
      {
        return;
      }
      _undoHistory.Push(_moveHistory.Pop());
      Current = _moveHistory.Count > 0 ? _moveHistory.Peek() : InitialDirection;
    }

    public void Redo()
    {
      if (_undoHistory.Count <= 0)
      {
        return;
      }
      _moveHistory.Push(_undoHistory.Pop());
      Current = _moveHistory.Peek();
    }
  }
}
