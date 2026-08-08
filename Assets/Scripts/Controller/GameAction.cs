public abstract record GameAction
{
  private GameAction() { }

  public sealed record Move(Direction Direction) : GameAction;

  public sealed record Undo : GameAction;

  public sealed record Redo : GameAction;
}
