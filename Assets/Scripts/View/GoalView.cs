using UnityEngine;

public class GoalView : MonoBehaviour, IStageObjectView
{
  public void TeleportTo(int x, int y, int z)
  {
    transform.localPosition = new Vector3(x, y, z);
  }
}
