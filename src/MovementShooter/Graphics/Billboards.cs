using System;
using Microsoft.Xna.Framework;

namespace MovementShooter.Graphics;

/// <summary>
/// Builds camera-facing world matrices for billboarded quads.
///
/// Shared by the health bars, the rocket trails and the explosion flashes, because getting the basis wrong is
/// silent and nasty: a matrix whose rows are not a full orthonormal basis is singular, and although the vertex
/// *positions* still come out right, the engine cannot invert it to transform normals. Lighting a mesh with a
/// singular world matrix produces non-finite normals, which shade to black. The result is a black quad hanging
/// in the world that turns with the camera - which looks exactly like a rendering bug and is one.
/// </summary>
public static class Billboards
{
    /// <summary>
    /// A world matrix that places a unit quad (centred on the origin, facing +Z) at <paramref name="centre"/>,
    /// oriented to face <paramref name="eye"/>, and scaled to the given size.
    /// </summary>
    /// <param name="view">The active view matrix, used for the camera's roll and for the in-plane axes.</param>
    /// <param name="eye">The camera's world position. Required, because "facing the camera" is only unambiguous if
    /// the camera's position is known - deriving it from the view matrix alone means guessing which basis vector is
    /// the viewing direction and which its negation.</param>
    /// <param name="centre">World position of the quad's centre.</param>
    /// <param name="width">Full width in metres.</param>
    /// <param name="height">Full height in metres.</param>
    /// <param name="upOverride">
    /// Optional axis to use as the quad's up, instead of the camera's. This is what makes a trail stretch along its
    /// direction of travel rather than always facing the eye squarely.
    /// </param>
    public static Matrix FaceCamera(Matrix view, Vector3 eye, Vector3 centre, float width, float height, Vector3? upOverride = null)
    {
        // The camera's basis comes from transforming world axes by the view matrix, which is exact by construction
        // and avoids depending on how any particular look-at laid out its rows.
        Vector3 cameraUp = Normalized(Vector3.TransformNormal(Vector3.Up, view));
        Vector3 cameraBack = Normalized(Vector3.TransformNormal(Vector3.Backward, view));

        Vector3 up = upOverride ?? cameraUp;

        // The quad's normal points from the quad back towards the eye. This is what survives back-face culling: the world
        // culls clockwise triangles, so a normal pointing away from the camera makes the quad invisible.
        Vector3 back = Normalized(eye - centre);

        // If the caller has put the quad exactly at the eye, or the override axis is parallel to the view, fall back
        // to the camera's own back vector so the result is always well defined.
        if (back.LengthSquared() < 0.5f)
        {
            back = cameraBack;
        }

        Vector3 across = Vector3.Cross(up, back);

        if (across.LengthSquared() < 1e-6f)
        {
            across = Vector3.Cross(cameraUp, cameraBack);
        }

        across = Normalized(across);
        up = Normalized(Vector3.Cross(back, across));

        // All three rows are a proper orthonormal basis, so the matrix is invertible and its normals survive
        // lighting. That invertibility is the entire point of this helper: a zeroed third row still positions the
        // quad correctly but cannot be inverted to transform normals, which is what rendered the health bars black.
        Matrix basis = new(
            across.X, across.Y, across.Z, 0f,
            up.X, up.Y, up.Z, 0f,
            back.X, back.Y, back.Z, 0f,
            0f, 0f, 0f, 1f);

        // The quad mesh is one unit across and one unit tall, so the scale is half the requested size.
        return Matrix.CreateScale(width * 0.5f, height * 0.5f, 1f) * basis * Matrix.CreateTranslation(centre);
    }

    private static Vector3 Normalized(Vector3 value) =>
        value.LengthSquared() < 1e-8f ? Vector3.Forward : Vector3.Normalize(value);
}
