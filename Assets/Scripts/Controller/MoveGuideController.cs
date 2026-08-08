using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class MoveGuideController : IDisposable
{
  private readonly MoveGuideView view;
  private readonly StageCameraRig cameraRig;
  private readonly CancellationTokenSource cts = new();

  private MoveMapping lastMapping;
  private CancellationTokenSource flashCts;
  private bool wasDragging;

  public MoveGuideController(MoveGuideView view, StageCameraRig cameraRig)
  {
    this.view = view;
    this.cameraRig = cameraRig;

    lastMapping = cameraRig.CurrentMapping;
    ApplyLabels(lastMapping);
    view.FaceCamera(cameraRig.Rotation);

    UpdateLoop(cts.Token).Forget();
    Flash();
  }

  private async UniTaskVoid UpdateLoop(CancellationToken ct)
  {
    while (true)
    {
      await UniTask.Yield(PlayerLoopTiming.Update, ct);

      view.FaceCamera(cameraRig.Rotation);

      var mapping = cameraRig.CurrentMapping;
      if (mapping != lastMapping)
      {
        lastMapping = mapping;
        ApplyLabels(mapping);
        Flash();
      }

      var isDragging = cameraRig.IsDragging;
      if (isDragging && !wasDragging)
      {
        Flash();
      }
      wasDragging = isDragging;
    }
  }

  private void ApplyLabels(MoveMapping mapping)
  {
    view.SetLabels(
      Letter(mapping.ScreenDirectionFor(Direction.Forward)),
      Letter(mapping.ScreenDirectionFor(Direction.Right)),
      Letter(mapping.ScreenDirectionFor(Direction.Back)),
      Letter(mapping.ScreenDirectionFor(Direction.Left))
    );
  }

  private static string Letter(ScreenDirection screenDirection) => screenDirection switch
  {
    ScreenDirection.Up => "W",
    ScreenDirection.Down => "S",
    ScreenDirection.Left => "A",
    ScreenDirection.Right => "D",
    _ => throw new NotSupportedException($"Unknown screen direction {screenDirection}."),
  };

  private void Flash()
  {
    flashCts?.Cancel();
    flashCts?.Dispose();
    flashCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
    FadeRoutine(flashCts.Token).Forget();
  }

  private async UniTaskVoid FadeRoutine(CancellationToken ct)
  {
    view.SetAlpha(1f);

    while (cameraRig.IsDragging)
    {
      await UniTask.Yield(PlayerLoopTiming.Update, ct);
    }

    await UniTask.Delay(TimeSpan.FromSeconds(view.VisibleSeconds), cancellationToken: ct);

    for (float elapsed = 0f; elapsed < view.FadeOutSeconds; elapsed += Time.deltaTime)
    {
      view.SetAlpha(1f - elapsed / view.FadeOutSeconds);
      await UniTask.Yield(PlayerLoopTiming.Update, ct);
    }
    view.SetAlpha(0f);
  }

  public void Dispose()
  {
    flashCts?.Cancel();
    flashCts?.Dispose();
    cts.Cancel();
    cts.Dispose();
  }
}
