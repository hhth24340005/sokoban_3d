using UnityEngine;

public class PlayerView : MovableView
{
  [SerializeField]
  private MoveGuideView moveGuide;

  public MoveGuideView MoveGuide => moveGuide;
}
