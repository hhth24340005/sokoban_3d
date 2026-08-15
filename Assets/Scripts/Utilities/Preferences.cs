using System;

public sealed class Preferences
{
  public State Default { get; }
  public State Current { get; private set; }

  // old, new
  public event Action<State, State> OnChanged;

  private Preferences(State init)
  {
    Default = init;
    Current = init;
  }

  public static Preferences Of(DefaultPreferences def)
  {
    return new(
      new(
        CameraYawDegreesPerPixel: def.CameraYawDegreesPerPixel,
        CameraPitchDegreesPerPixel: def.CameraPitchDegreesPerPixel
      )
    );
  }

  public void Update(Func<State, State> modify)
  {
    var next = modify(Current);
    if (Current == next)
    {
      return;
    }
    var old = Current;
    Current = next;
    OnChanged?.Invoke(old, next);
  }

  public sealed record State(
    float CameraYawDegreesPerPixel,
    float CameraPitchDegreesPerPixel
  );

}
