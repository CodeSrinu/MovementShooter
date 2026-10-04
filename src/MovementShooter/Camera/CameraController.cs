using Microsoft.Xna.Framework;
using MovementShooter.App;

namespace MovementShooter.Camera;

/// <summary>
/// Owns the available camera rigs, translates <see cref="CameraInput"/> snapshots into camera
/// behaviour, and exposes the matrices the renderer needs.
/// </summary>
public sealed class CameraController
{
    private readonly CameraInput _input;
    private readonly FlyCamera _flyCamera;
    private readonly OrbitCamera _orbitCamera;
    private CameraRig _active;

    public CameraController(AppConfig config)
    {
        _input = new CameraInput(config.MouseSensitivity);
        _flyCamera = new FlyCamera(config.FieldOfViewDegrees, config.NearPlane, config.FarPlane, config.FlyCameraSpeed);
        _orbitCamera = new OrbitCamera(config.FieldOfViewDegrees, config.NearPlane, config.FarPlane);
        _active = _flyCamera;
    }

    /// <summary>Movement multiplier while the boost key is held.</summary>
    public float BoostMultiplier { get; } = 4f;

    public void Update(float deltaSeconds, bool acceptMouseLook)
    {
        _input.Poll(acceptMouseLook);

        if (_input.ToggleCameraRequested)
        {
            _active = ReferenceEquals(_active, _flyCamera) ? _orbitCamera : _flyCamera;
        }

        if (_input.ResetRequested)
        {
            _active.Reset();
        }

        _active.Look(_input.LookDeltaYaw, _input.LookDeltaPitch);
        _active.Zoom(_input.ZoomDelta);

        Vector3 local = _input.MoveDirection;
        _active.SpeedMultiplier = _input.Boost ? BoostMultiplier : 1f;

        if (local != Vector3.Zero)
        {
            Vector3 direction = (_active.Forward * local.Z) +
                                (_active.Right * local.X) +
                                (Vector3.Up * local.Y);
            _active.Move(direction, deltaSeconds);
        }
    }

    public Matrix GetViewMatrix() => _active.GetViewMatrix();

    public Matrix GetProjectionMatrix(float aspectRatio) => _active.GetProjectionMatrix(aspectRatio);

    public string Describe() =>
        $"{_active.Name} | pos {Format(_active.Position)} | fov {_active.FieldOfViewDegrees:0}";

    private static string Format(Vector3 value) =>
        $"{value.X:0.#}, {value.Y:0.#}, {value.Z:0.#}";
}