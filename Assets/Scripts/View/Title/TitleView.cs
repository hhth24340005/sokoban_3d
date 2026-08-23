using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleView : MonoBehaviour
{
  [SerializeField]
  private CanvasGroup overlay;

  [SerializeField]
  private Ease overlayEase = Ease.InQuad;

  [SerializeField]
  private float overlayShowSeconds = 1.5f;

  [SerializeField]
  private AudioSource music;

  [SerializeField]
  private AudioSource submitSound;

  [SerializeField]
  private AudioSource cancelSound;

  [SerializeField]
  private CanvasGroup initialButtonGroup;

  [SerializeField]
  private Button startButton;

  [SerializeField]
  private Button settingsButton;

  [SerializeField]
  private Button creditsButton;

  [SerializeField]
  private CanvasGroup stageSelectionGroup;

  [SerializeField]
  private List<StageEntry> stages;

  [SerializeField]
  private Button cancelStartButton;

  [SerializeField]
  private CanvasGroup creditsGroup;

  [SerializeField]
  private Button creditsBackButton;

  [SerializeField]
  private TransitionView startTransitionPrefab;

  public async UniTask<
    (GameStagePreset result, Func<CancellationToken, UniTask> fadeIn)
  > PlayAsync(
    Transform parent,
    Preferences pref,
    Func<CancellationToken, UniTask> fadeIn,
    CancellationToken ct
  )
  {
    using (parent.CreateChild(this, out var instantiated, copyIfExisting: false))
    {
      await fadeIn(ct);
      instantiated.music.Play();
      await DOTween.To(
        () => instantiated.overlay.alpha,
        x => instantiated.overlay.alpha = x,
        1f,
        overlayShowSeconds
      ).SetEase(overlayEase)
        .WithCancellation(ct);
      instantiated.overlay.blocksRaycasts = true;
      instantiated.overlay.interactable = true;
      return await instantiated.WaitForActionAsync(parent, ct);
    }
  }

  private async UniTask<
    (GameStagePreset, Func<CancellationToken, UniTask>)
 > WaitForActionAsync(
    Transform parent,
    CancellationToken ct
  )
  {
    var tcs =
      new UniTaskCompletionSource<
        (
          GameStagePreset,
          Func<
            CancellationToken,
            UniTask<Func<CancellationToken, UniTask>>
          >
        )
      >();

    while (true)
    {
      var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      var (hasResult, (stage, fadeOut)) = await UniTask.WhenAny(
        tcs.Task,
        UniTask.Create(async () =>
        {
          var cts1 =
            CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
          var (_, task) =
            await UniTask.WhenAny<Func<CancellationToken, UniTask>>(
              WaitForStart(parent, tcs, cts1.Token),
              WaitForShowCreditsAsync(cts1.Token)
            );
          cts1.Cancel();
          await task(cts.Token);
        })
      );
      cts.Cancel();
      if (hasResult)
      {
        return (stage, await fadeOut(ct));
      }
    }
  }

  private async UniTask<Func<CancellationToken, UniTask>> WaitForStart(
    Transform transitionParent,
    UniTaskCompletionSource<
      (
        GameStagePreset,
        Func<
          CancellationToken,
          UniTask<Func<CancellationToken, UniTask>>
        >
      )
   > tcs,
    CancellationToken ct
  )
  {
    await startButton.OnClickAsync(ct);
    submitSound.Play();
    return async ct1 =>
    {
      initialButtonGroup.blocksRaycasts = false;
      initialButtonGroup.interactable = false;
      initialButtonGroup.alpha = 0f;

      stageSelectionGroup.blocksRaycasts = true;
      stageSelectionGroup.interactable = true;
      stageSelectionGroup.alpha = 1f;

      var (hasResult, (_, stage)) = await UniTask.WhenAny(
        UniTask.WhenAny(
          stages.Select(entry =>
            entry.Button
              .OnClickAsync(ct1)
              .ContinueWith(() =>
              {
                submitSound.Play();
                return entry.Stage;
              })
          ).ToImmutableArray()
        ),
        cancelStartButton
          .OnClickAsync(ct1)
          .ContinueWith(() => cancelSound.Play())
      );
      if (hasResult)
      {
        transitionParent.CreateChild(startTransitionPrefab, out var transition);
        tcs.TrySetResult((stage, transition.CoverAsync));
      }
      else
      {
        initialButtonGroup.blocksRaycasts = true;
        initialButtonGroup.interactable = true;
        initialButtonGroup.alpha = 1f;
      }
      stageSelectionGroup.blocksRaycasts = false;
      stageSelectionGroup.interactable = false;
      stageSelectionGroup.alpha = 0f;
    };
  }

  private async UniTask<Func<CancellationToken, UniTask>> WaitForShowCreditsAsync(
    CancellationToken ct
  )
  {
    await creditsButton.OnClickAsync(ct);
    submitSound.Play();
    return async ct1 =>
    {
      initialButtonGroup.blocksRaycasts = false;
      initialButtonGroup.interactable = false;
      initialButtonGroup.alpha = 0f;
      creditsGroup.blocksRaycasts = true;
      creditsGroup.interactable = true;
      creditsGroup.alpha = 1f;
      await creditsBackButton.OnClickAsync(ct1);
      cancelSound.Play();
      creditsGroup.blocksRaycasts = false;
      creditsGroup.interactable = false;
      creditsGroup.alpha = 0f;
      initialButtonGroup.blocksRaycasts = true;
      initialButtonGroup.interactable = true;
      initialButtonGroup.alpha = 1f;
    };
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
