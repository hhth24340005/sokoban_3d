using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class MovableView :
  MonoBehaviour,
  IMovableView
{
  [SerializeField]
  private float moveSeconds = 0.5f;

  public void TeleportTo(int x, int y, int z)
  {
    transform.localPosition = new Vector3(x, y, z);
  }

  public async UniTask MoveTo(
    int x,
    int y,
    int z,
    MoveMode moveMode,
    CancellationToken ct
  )
  {
    var originalPos = transform.localPosition;
    var goalPos = new Vector3(x, y, z);
    try
    {
      for (float elapsed = 0f; elapsed < moveSeconds; elapsed += Time.deltaTime)
      {
        transform.localPosition = Vector3.Lerp(originalPos, goalPos, elapsed / moveSeconds);
        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: ct);
      }
    }
    finally
    {
      transform.localPosition = goalPos;
    }
  }
}
