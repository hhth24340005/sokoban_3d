using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PauseView : MonoBehaviour
{
  [SerializeField]
  private CanvasGroup overlay;

  [SerializeField]
  private float fadeInSeconds = 0.1f;

  [SerializeField]
  private float fadeOutSeconds = 0.2f;

  [SerializeField]
  private PauseViewButtonGroup buttonGroup;

  public async UniTask<Result> ShowAndWaitForAction(
    PlayerInputActions.GameActions gameInput,
    CancellationToken ct
  )
  {
    using var inputActions = new PlayerInputActions();
    var uiInput = inputActions.UI;
    bool resume = true;
    try
    {
      gameInput.Disable();
      uiInput.Enable();
      gameObject.SetActive(true);

      await DOTween.To(
        getter: () => overlay.alpha,
        setter: x => overlay.alpha = x,
        endValue: 1f,
        duration: fadeInSeconds
      ).WithCancellation(ct);
      overlay.interactable = true;
      await buttonGroup.FadeInAsync(ct);

      var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      var tcs = new UniTaskCompletionSource<Func<CancellationToken, UniTask<Result>>>();

      WaitForResumeAction(uiInput, tcs, cts.Token).Forget();
      WaitForReturnToTitleAction(tcs, ct).Forget();
      var anim = await tcs.Task;
      cts.Cancel();
      overlay.interactable = false;
      var ret = await anim(ct);
      resume = ret switch
      {
        Result.ReturnToTitle => false,
        _ => true,
      };

      return ret;
    }
    finally
    {
      overlay.interactable = false;
      uiInput.Disable();
      if (resume)
      {
        gameObject.SetActive(false);
        gameInput.Enable();
      }
    }
  }

  private async UniTask WaitForResumeAction(
    PlayerInputActions.UIActions inputAction,
    UniTaskCompletionSource<Func<CancellationToken, UniTask<Result>>> tcs,
    CancellationToken ct
  )
  {
    var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    var buttonTask = buttonGroup.WaitForResumeButtonClickAsync(ct);
    var cancelInputTask = WaitForCancelInputAsync(inputAction, cts.Token);

    await UniTask.WhenAny(buttonTask, cancelInputTask);
    cts.Cancel();

    tcs.TrySetResult(
      async (ct) =>
      {
        await buttonGroup.FadeOutAsync(ct);

        await DOTween.To(
          getter: () => overlay.alpha,
          setter: x => overlay.alpha = x,
          endValue: 0f,
          duration: fadeOutSeconds
        ).WithCancellation(ct);

        return new Result.Resume();
      }
    );
  }

  private async UniTask WaitForCancelInputAsync(
    PlayerInputActions.UIActions inputAction,
    CancellationToken ct
  )
  {
    var buttonTask = buttonGroup.WaitForResumeButtonClickAsync(ct);
    var tcs = new UniTaskCompletionSource();
    void OnPerform(InputAction.CallbackContext ctx) => tcs.TrySetResult();
    using var _ = ct.Register(() => tcs.TrySetCanceled(ct));
    try
    {
      inputAction.Cancel.performed += OnPerform;
      await tcs.Task;
    }
    finally
    {
      inputAction.Cancel.performed -= OnPerform;
    }
  }

  private async UniTask WaitForReturnToTitleAction(
    UniTaskCompletionSource<Func<CancellationToken, UniTask<Result>>> tcs,
    CancellationToken ct
  )
  {
    await buttonGroup.WaitForReturnButtonClickAsync(ct);
    tcs.TrySetResult((ct) => UniTask.FromResult((Result)new Result.ReturnToTitle()));
  }

  public abstract record Result
  {
    private Result() { }

    public sealed record Resume : Result;

    public sealed record ReturnToTitle : Result;
  }
}
