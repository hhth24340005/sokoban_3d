using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class StageCameraRig : IDisposable
{
  private readonly CameraSettings settings;
  private readonly Camera camera;
  private readonly ScopedGameObject headlight;
  private readonly InputAction dragButton;
  private readonly InputAction pointerDelta;
  private readonly InputAction scroll;
  private readonly CancellationTokenSource cts = new();

  private OrbitCamera state;

  public StageCameraRig(Camera camera, Vector3 pivot, CameraSettings settings)
  {
    this.camera = camera;
    this.settings = settings;
    state = new OrbitCamera(
      pivot,
      settings.InitialYawDegrees,
      settings.InitialPitchDegrees,
      settings.InitialDistance,
      settings.MinPitchDegrees,
      settings.MaxPitchDegrees,
      settings.MinDistance,
      settings.MaxDistance
    );
    state.ApplyTo(camera);

    headlight = camera.gameObject.ChildOf("Headlight").With<Light>(out var headlightLight);
    headlightLight.type = LightType.Directional;
    headlightLight.intensity = settings.HeadlightIntensity;
    headlightLight.shadows = LightShadows.None;
    headlightLight.transform.localRotation = Quaternion.identity;

    dragButton = new InputAction(type: InputActionType.Button, binding: "<Mouse>/leftButton");
    dragButton.Enable();
    pointerDelta = new InputAction(type: InputActionType.Value, binding: "<Mouse>/delta");
    pointerDelta.Enable();
    scroll = new InputAction(type: InputActionType.Value, binding: "<Mouse>/scroll/y");
    scroll.Enable();

    UpdateLoop(cts.Token).Forget();
  }

  public MoveMapping CurrentMapping => MoveMapping.FromYawDegrees(state.YawDegrees);

  public Quaternion Rotation => state.Rotation;

  public bool IsDragging => dragButton.IsPressed() && !IsPointerOverUI();

  private async UniTaskVoid UpdateLoop(CancellationToken ct)
  {
    while (true)
    {
      await UniTask.Yield(PlayerLoopTiming.Update, ct);
      Tick();
    }
  }

  private void Tick()
  {
    var next = state;

    var scrollDelta = scroll.ReadValue<float>();
    if (scrollDelta != 0f)
    {
      next = next.Zoomed(-scrollDelta / 120f * settings.DistancePerScrollNotch);
    }

    if (IsDragging)
    {
      var delta = pointerDelta.ReadValue<Vector2>();
      next = next.Rotated(
        delta.x * settings.YawDegreesPerPixel,
        -delta.y * settings.PitchDegreesPerPixel
      );
    }

    state = next;
    state.ApplyTo(camera);
  }

  private static bool IsPointerOverUI() =>
    EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

  public void Dispose()
  {
    cts.Cancel();
    cts.Dispose();
    headlight.Dispose();
    dragButton.Dispose();
    pointerDelta.Dispose();
    scroll.Dispose();
  }
}
