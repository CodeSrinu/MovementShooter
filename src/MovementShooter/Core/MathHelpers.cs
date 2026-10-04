using System;

namespace MovementShooter.Core;

/// <summary>
/// Scalar helpers shared by the cameras today and by movement code later. Nothing lives here until a
/// caller needs it.
/// </summary>
public static class MathHelpers
{
    public static float Clamp(float value, float min, float max) =>
        value < min ? min : value > max ? max : value;
}