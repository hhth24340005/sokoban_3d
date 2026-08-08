using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;

public sealed class KeyboardDirectionInput : IDisposable
{
  private static readonly (string binding, ScreenDirection direction)[] keyMap =
  {
    ("<Keyboard>/w", ScreenDirection.Up),
    ("<Keyboard>/s", ScreenDirection.Down),
    ("<Keyboard>/a", ScreenDirection.Left),
    ("<Keyboard>/d", ScreenDirection.Right),
  };

  private readonly List<InputAction> actions = new();
  private readonly List<ScreenDirection> held = new();
  private ScreenDirection? buffered;
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

  public async UniTask<ScreenDirection> NextStepDirection(CancellationToken ct)
  {
    while (true)
    {
      if (buffered is ScreenDirection pressed)
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

  private void OnPressed(ScreenDirection direction)
  {
    held.Remove(direction);
    held.Add(direction);
    buffered = direction;
    waiter?.TrySetResult();
    waiter = null;
  }

  private void OnReleased(ScreenDirection direction)
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
