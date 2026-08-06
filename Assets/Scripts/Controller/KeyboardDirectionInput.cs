using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;

public sealed class KeyboardDirectionInput : IDisposable
{
  private static readonly (string binding, Direction direction)[] keyMap =
  {
    ("<Keyboard>/w", Direction.Forward),
    ("<Keyboard>/s", Direction.Back),
    ("<Keyboard>/a", Direction.Left),
    ("<Keyboard>/d", Direction.Right),
  };

  private readonly List<InputAction> actions = new();
  private readonly List<Direction> held = new();
  private Direction? buffered;
  private UniTaskCompletionSource waiter;

  public KeyboardDirectionInput()
  {
    foreach (var (binding, direction) in keyMap)
    {
      var action = new InputAction(type: InputActionType.Button, binding: binding);
      action.performed += _ => OnPressed(direction);
      action.canceled += _ => OnReleased(direction);
      action.Enable();
      actions.Add(action);
    }
  }

  public async UniTask<Direction> NextStepDirection(CancellationToken ct)
  {
    while (true)
    {
      if (buffered is Direction pressed)
      {
        buffered = null;
        return pressed;
      }
      if (0 < held.Count)
      {
        return held[^1];
      }

      waiter = new UniTaskCompletionSource();
      await waiter.Task.AttachExternalCancellation(ct);
    }
  }

  private void OnPressed(Direction direction)
  {
    held.Remove(direction);
    held.Add(direction);
    buffered = direction;
    waiter?.TrySetResult();
    waiter = null;
  }

  private void OnReleased(Direction direction)
  {
    held.Remove(direction);
  }

  public void Dispose()
  {
    foreach (var action in actions)
    {
      action.Dispose();
    }
    actions.Clear();
    waiter?.TrySetCanceled();
    waiter = null;
  }
}
