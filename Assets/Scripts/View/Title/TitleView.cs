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
  private List<StageEntry> stages;

  [SerializeField]
  private TransitionView startTransitionPrefab;

  public async UniTask<(GameStagePreset result, Func<CancellationToken, UniTask> fadeIn)> PlayAsync(
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

  private async UniTask<(GameStagePreset, Func<CancellationToken, UniTask>)> WaitForActionAsync(
    Transform parent,
    CancellationToken ct
  )
  {
    var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    var tcs = new UniTaskCompletionSource<(GameStagePreset, Func<CancellationToken, UniTask<Func<CancellationToken, UniTask>>>)>();
    WaitForStart(parent, tcs, cts.Token).Forget();
    (var ret, var fadeOut) = await tcs.Task;
    cts.Cancel();
    var fadeIn = await fadeOut(ct);
    return (ret, fadeIn);
  }

  private async UniTask WaitForStart(
    Transform parent,
    UniTaskCompletionSource<(GameStagePreset, Func<CancellationToken, UniTask<Func<CancellationToken, UniTask>>>)> tcs,
    CancellationToken ct
  )
  {
    await startButton.OnClickAsync(cancellationToken: ct);
    parent.CreateChild(startTransitionPrefab, out var transition);
    tcs.TrySetResult((stages.First().Stage, transition.CoverAsync));
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
