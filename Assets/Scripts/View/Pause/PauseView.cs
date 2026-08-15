using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PauseView : MonoBehaviour
{
  [SerializeField]
  private Button resumeButton;

  [SerializeField]
  private Button returnToTitleButton;

  public async UniTask<Result> ShowAndWaitForAction(
    PlayerInputActions.GameActions gameInput,
    CancellationToken ct
  )
  {
    using var inputActions = new PlayerInputActions();
    var uiInput = inputActions.UI;
    try
    {
      gameInput.Disable();
      uiInput.Enable();
      gameObject.SetActive(true);
      var resumeTask = WaitForResumeAction(uiInput, ct);
      var returnTask = WaitForReturnToTitleAction(ct);
      (_, var ret) = await UniTask.WhenAny<Result>(resumeTask, returnTask);
      return ret;
    }
    finally
    {
      gameObject.SetActive(false);
      uiInput.Disable();
      gameInput.Enable();
    }
  }

  private async UniTask<Result> WaitForResumeAction(
    PlayerInputActions.UIActions inputAction,
    CancellationToken ct
  )
  {
    var buttonTask = resumeButton.OnClickAsync(ct);
    var tcs = new UniTaskCompletionSource();
    void OnPerform(InputAction.CallbackContext ctx) => tcs.TrySetResult();
    using var _ = ct.Register(() => tcs.TrySetCanceled(ct));
    inputAction.Cancel.performed += OnPerform;
    try
    {
      await UniTask.WhenAny(buttonTask, tcs.Task);
    }
    finally
    {
      inputAction.Cancel.performed -= OnPerform;
    }
    return new Result.Resume();
  }

  private async UniTask<Result> WaitForReturnToTitleAction(CancellationToken ct)
  {
    await returnToTitleButton.OnClickAsync(ct);
    return new Result.ReturnToTitle();
  }

  public abstract record Result
  {
    private Result() { }

    public sealed record Resume : Result;

    public sealed record ReturnToTitle : Result;
  }
}
