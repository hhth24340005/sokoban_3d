using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleView : MonoBehaviour
{
  [SerializeField]
  private Button startButton;

  [SerializeField]
  private Button quitButton;

  [SerializeField]
  private List<StageEntry> stages;

  [SerializeField]
  private TransitionView startTransitionPrefab;

  public async UniTask<(Result result, Func<CancellationToken, UniTask> fadeIn)> PlayAsync(
    Transform parent,
    Preferences pref,
    Func<CancellationToken, UniTask> fadeIn,
    CancellationToken ct
  )
  {
    using (parent.CreateChild(this, out var instantiated, copyIfExisting: false))
    {
      await fadeIn(ct);
      return await instantiated.WaitForActionAsync(parent, ct);
    }
  }

  private async UniTask<(Result, Func<CancellationToken, UniTask>)> WaitForActionAsync(
    Transform parent,
    CancellationToken ct
  )
  {
    var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    var tcs = new UniTaskCompletionSource<(Result, Func<CancellationToken, UniTask<Func<CancellationToken, UniTask>>>)>();
    WaitForStart(parent, tcs, cts.Token).Forget();
    WaitForQuitAsync(tcs, cts.Token).Forget();
    (var ret, var fadeOut) = await tcs.Task;
    cts.Cancel();
    var fadeIn = await fadeOut(ct);
    return (ret, fadeIn);
  }

  private async UniTask WaitForStart(
    Transform parent,
    UniTaskCompletionSource<(Result, Func<CancellationToken, UniTask<Func<CancellationToken, UniTask>>>)> tcs,
    CancellationToken ct
  )
  {
    await startButton.OnClickAsync(cancellationToken: ct);
    parent.CreateChild(startTransitionPrefab, out var transition);
    tcs.TrySetResult((new Result.Start(stages.First().Stage), transition.CoverAsync));
  }

  private async UniTask WaitForQuitAsync(
    UniTaskCompletionSource<(Result, Func<CancellationToken, UniTask<Func<CancellationToken, UniTask>>>)> tcs,
    CancellationToken ct
  )
  {
    await quitButton.OnClickAsync(cancellationToken: ct);
    tcs.TrySetResult((
      new Result.QuitGame(),
      (_) => UniTask.FromResult<Func<CancellationToken, UniTask>>((_) => UniTask.CompletedTask)
    ));
  }

  public abstract record Result
  {
    private Result() { }

    public sealed record Start(GameStagePreset Stage) : Result;

    public sealed record QuitGame : Result;
  }

  [Serializable]
  public struct StageEntry
  {
    [SerializeField]
    private Button button;

    [SerializeField]
    private GameStagePreset stage;

    public readonly Button Button => button;

    public readonly GameStagePreset Stage => stage;
  }
}
