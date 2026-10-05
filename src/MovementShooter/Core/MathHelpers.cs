namespace MovementShooter.Core;

/// <summary>
/// Scalar helpers shared by the cameras and the player controller. Nothing lives here until a caller
/// needs it.
/// </summary>
public static class MathHelpers
{
    public const float Epsilon = 1e-6f;

    public static float Clamp(float value, float min, float max) =>
        value < min ? min : value > max ? max : value;

    public static float Clamp01(float value) => Clamp(value, 0f, 1f);
}