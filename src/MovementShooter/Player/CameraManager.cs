using System;
using Microsoft.Xna.Framework;

namespace MovementShooter.Player;

/// <summary>
/// Camera mode for the player: first-person or third-person.
/// </summary>
public enum CameraMode
{
    FirstPerson,
    ThirdPerson,
}

/// <summary>
/// Manages the camera mode and provides the appropriate camera matrices for rendering.
/// </summary>
public sealed class CameraManager : IDisposable
{
    private readonly PlayerCamera _firstPersonCamera;
    private readonly PlayerCamera _thirdPersonCamera;
    private CameraMode _currentMode;
    private bool _disposed;

    public CameraMode CurrentMode => _currentMode;

    public event Action<CameraMode>? OnModeChanged;

    public CameraManager(PlayerTuning tuning)
    {
        _firstPersonCamera = new PlayerCamera(tuning);
        _thirdPersonCamera = new PlayerCamera(tuning);
        _currentMode = CameraMode.FirstPerson;
    }

    /// <summary>
    /// Switches the camera mode. Preserves player state (position, velocity, etc.).
    /// </summary>
    public void SwitchMode(CameraMode mode)
    {
        if (_currentMode == mode)
            return;

        _currentMode = mode;
        OnModeChanged?.Invoke(mode);
    }

    /// <summary>
    /// Toggles between first-person and third-person.
    /// </summary>
    public void ToggleMode()
    {
        SwitchMode(_currentMode == CameraMode.FirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson);
    }

    /// <summary>
    /// Updates both cameras. Only the active one tracks the player.
    /// </summary>
    public void Update(float deltaSeconds, Vector3 playerPosition, float stanceHeight, float yaw, bool isSliding, Vector3 velocity)
    {
        // Update the active camera
        if (_currentMode == CameraMode.FirstPerson)
        {
            _firstPersonCamera.Follow(playerPosition, 1.6f);
        }
        else
        {
            // Third-person camera: behind and above the player
            _thirdPersonCamera.Follow(playerPosition + new Vector3(0, 2f, -5f), 1.6f);
        }
    }

    /// <summary>
    /// Gets the view matrix for the current camera mode.
    /// </summary>
    public Matrix GetViewMatrix()
    {
        return _currentMode == CameraMode.FirstPerson
            ? _firstPersonCamera.GetViewMatrix()
            : _thirdPersonCamera.GetViewMatrix();
    }

    /// <summary>
    /// Gets the projection matrix for the current camera.
    /// </summary>
    public Matrix GetProjectionMatrix(float aspectRatio)
    {
        if (_currentMode == CameraMode.FirstPerson)
        {
            return _firstPersonCamera.GetProjectionMatrix(aspectRatio);
        }
        else
        {
            return _thirdPersonCamera.GetProjectionMatrix(aspectRatio);
        }
    }

    /// <summary>
    /// Gets the current camera's position.
    /// </summary>
    public Vector3 Position => _currentMode == CameraMode.FirstPerson
        ? _firstPersonCamera.Position
        : _thirdPersonCamera.Position;

    /// <summary>
    /// Gets the current camera's forward direction.
    /// </summary>
    public Vector3 Forward => _currentMode == CameraMode.FirstPerson
        ? _firstPersonCamera.Forward
        : _thirdPersonCamera.Forward;

    /// <summary>
    /// Gets the current camera's yaw.
    /// </summary>
    public float Yaw
    {
        get => _currentMode == CameraMode.FirstPerson
            ? _firstPersonCamera.Yaw
            : _thirdPersonCamera.Yaw;
        set
        {
            if (_currentMode == CameraMode.FirstPerson)
            {
                _firstPersonCamera.Yaw = value;
            }
            else
            {
                _thirdPersonCamera.Yaw = value;
            }
        }
    }

    /// <summary>
    /// Gets the current camera's pitch.
    /// </summary>
    public float Pitch => _currentMode == CameraMode.FirstPerson
        ? _firstPersonCamera.Pitch
        : _thirdPersonCamera.Pitch;

    /// <summary>
    /// Applies a look delta to the active camera.
    /// </summary>
    public void ApplyLook(Vector2 mouseDelta)
    {
        if (_currentMode == CameraMode.FirstPerson)
        {
            _firstPersonCamera.ApplyLook(new Vector2(
                -mouseDelta.X, // invert X for proper rotation
                mouseDelta.Y)); // Y is inverted in input
        }
        else
        {
            // In third person, mouse rotates the camera around the player
            _thirdPersonCamera.Yaw -= 0.005f * 100; // placeholder
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // PlayerCamera doesn't implement IDisposable, so we don't call Dispose
        // _firstPersonCamera.Dispose();
        // _thirdPersonCamera.Dispose();
    }
}