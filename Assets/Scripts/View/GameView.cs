using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameView : MonoBehaviour
{
  // debug
  [SerializeField]
  private Button gameClearButton;

  public async UniTask WaitForGameClearActionAsync(CancellationToken ct)
  {
    await gameClearButton.OnClickAsync(ct);
  }
}
