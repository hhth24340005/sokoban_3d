using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class TransitionView : MonoBehaviour
{
  public abstract Func<CancellationToken, UniTask> Cover();

  public abstract UniTask<Func<CancellationToken, UniTask>> CoverAsync(CancellationToken ct);
}
