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

  public async UniTask<Result> PlayAsync(
    Transform parent,
    Preferences pref,
    CancellationToken ct
  )
  {
    using (parent.CreateChild(this, out var instantiated, copyIfExisting: false))
    {
      return await instantiated.WaitForActionAsync(ct);
    }
  }

  private async UniTask<Result> WaitForActionAsync(CancellationToken ct)
  {
    var start = WaitForStartAsync(ct);
    var quit = WaitForQuitAsync(ct);
    (_, var ret) = await UniTask.WhenAny<Result>(start, quit);
    return ret;
  }

  private async UniTask<Result> WaitForStartAsync(CancellationToken ct)
  {
    await startButton.OnClickAsync(cancellationToken: ct);
    return new Result.Start(stages.First().Stage);
  }

  private async UniTask<Result> WaitForQuitAsync(CancellationToken ct)
  {
    await quitButton.OnClickAsync(cancellationToken: ct);
    return new Result.QuitGame();
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
