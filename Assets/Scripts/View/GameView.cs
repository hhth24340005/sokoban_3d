using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameView : MonoBehaviour
{
  // debug
  [SerializeField]
  private Button gameClearButton;

  [SerializeField]
  private Button undoButton;

  [SerializeField]
  private Button redoButton;

  public async UniTask WaitForGameClearActionAsync(CancellationToken ct)
  {
    await gameClearButton.OnClickAsync(ct);
  }

  public async UniTask WaitForUndoActionAsync(CancellationToken ct)
  {
    await undoButton.OnClickAsync(ct);
  }

  public async UniTask WaitForRedoActionAsync(CancellationToken ct)
  {
    await redoButton.OnClickAsync(ct);
  }

  public void SetHistoryState(bool canUndo, bool canRedo)
  {
    undoButton.interactable = canUndo;
    redoButton.interactable = canRedo;
  }
}
