using System;
using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MovementShooter.Player;

namespace MovementShooter.UI;

/// <summary>
/// Development-only player readout: grounded state, position, velocity, horizontal speed and frame
/// timing. Deliberately minimal, and deliberately not a HUD - the real HUD is a later milestone.
/// </summary>
public sealed class PlayerDebugOverlay : IDisposable
{
    private const int LeftMargin = 12;
    private const int LineHeight = 11;

    private readonly TextRenderer _text;
    private readonly StringBuilder _builder = new();
    private bool _disposed;

    public PlayerDebugOverlay(GraphicsDevice device, BitmapFont font)
    {
        _text = new TextRenderer(device, font);
    }

    public bool Visible { get; set; } = true;

    public void Draw(
        MovementShooter.Player.Player player,
        float framesPerSecond,
        int subStepsLastFrame,
        Weapons.WeaponController? weapons = null,
        Combat.ExplosionSystem? explosions = null)
    {
        if (!Visible)
        {
            return;
        }

        Vector3 position = player.Position;
        Vector3 velocity = player.Velocity;
        int row = 0;

        Line("KINETIC - PLAYER DEBUG", ref row);

        _builder.Clear();
        _builder.Append("STATE    ").Append(player.Movement.State.ToString().ToUpperInvariant());
        _builder.Append("  GROUNDED ").Append(player.IsGrounded ? "YES" : "NO ");
        _builder.Append("  HEIGHT ").Append(Format(player.Movement.CurrentHeight));
        Line(_builder.ToString(), ref row);

        _builder.Clear();
        _builder.Append("HSPEED   ").Append(Format(player.HorizontalSpeed));
        _builder.Append("  VSPEED ").Append(Format(velocity.Y));
        _builder.Append("  CURSOR ").Append(player.IsCursorLocked ? "LOCKED" : "FREE");
        Line(_builder.ToString(), ref row);

        _builder.Clear();
        _builder.Append("SLIDE    ").Append(player.Movement.State == MovementState.Sliding ? "TIMER " + Format(player.Movement.SlideTimeRemaining) : "-");
        if (player.Movement.SlideBlockedFromStanding)
        {
            _builder.Append("  BLOCKED FROM STANDING");
        }

        Line(_builder.ToString(), ref row);

        _builder.Clear();
        DashStatus dash = player.Movement.Dash;
        _builder.Append("DASH     ").Append(player.Movement.IsDashing ? "BURST " + Format(player.Movement.DashTimeRemaining) : (dash.IsAvailable ? "READY" : "LOCKED"));
        _builder.Append("  CD ").Append(Format(dash.CooldownRemaining));
        _builder.Append("  AIR ").Append(dash.AirDashUsed ? "USED" : "-");
        _builder.Append("  AIR DASHES ").Append(dash.DashesThisAirbornePeriod);
        Line(_builder.ToString(), ref row);

        _builder.Clear();
        _builder.Append("FOV      ").Append(Format(player.Camera.FieldOfViewDegrees));
        _builder.Append("  BASE ").Append(Format(player.Tuning.FieldOfViewDegrees));
        Line(_builder.ToString(), ref row);

        _builder.Clear();
        _builder.Append("POS      ").Append(Format(position.X)).Append(' ').Append(Format(position.Y)).Append(' ').Append(Format(position.Z));
        Line(_builder.ToString(), ref row);

        _builder.Clear();
        _builder.Append("VEL      ").Append(Format(velocity.X)).Append(' ').Append(Format(velocity.Y)).Append(' ').Append(Format(velocity.Z));
        Line(_builder.ToString(), ref row);

        _builder.Clear();
        _builder.Append("COYOTE   ").Append(Format(player.Movement.CoyoteTimeRemaining));
        _builder.Append("  JUMP BUFFER ").Append(Format(player.Movement.JumpBufferRemaining));
        Line(_builder.ToString(), ref row);

        if (weapons is not null)
        {
            _builder.Clear();
            _builder.Append("HEALTH   ").Append(Format(player.Health.CurrentHealth));
            _builder.Append(" / ").Append(Format(player.Health.MaxHealth));
            _builder.Append(player.Health.IsDead ? "  DOWN" : "  OK");
            _builder.Append("  TAKEN ").Append(Format(player.Health.TotalDamageTaken));
            Line(_builder.ToString(), ref row);

            _builder.Clear();
            _builder.Append("WEAPON   ").Append(weapons.WeaponName);
            _builder.Append("  ROCKETS ").Append(weapons.LiveProjectiles.ToString(CultureInfo.InvariantCulture));
            _builder.Append("  FIRED ").Append(weapons.Weapon.ShotsFired.ToString(CultureInfo.InvariantCulture));
            _builder.Append("  CD ").Append(Format(weapons.Weapon.CooldownRemaining));
            Line(_builder.ToString(), ref row);
        }

        if (explosions is not null && explosions.LastExplosion is not null)
        {
            _builder.Clear();
            _builder.Append("LAST BOOM").Append(Format(explosions.LastExplosionSeconds));
            _builder.Append("s  AT ").Append(Format(explosions.LastExplosionCentre.X))
                .Append(' ').Append(Format(explosions.LastExplosionCentre.Y))
                .Append(' ').Append(Format(explosions.LastExplosionCentre.Z));
            _builder.Append("  R ").Append(Format(explosions.LastExplosionRadius));
            _builder.Append("  TOTAL ").Append(explosions.Detonations.ToString(CultureInfo.InvariantCulture));
            Line(_builder.ToString(), ref row);
        }

        _builder.Clear();
        _builder.Append("FPS ").Append(framesPerSecond.ToString("0", CultureInfo.InvariantCulture));
        _builder.Append("  SUBSTEPS ").Append(subStepsLastFrame.ToString(CultureInfo.InvariantCulture));
        Line(_builder.ToString(), ref row);
    }

    private void Line(string text, ref int row) => _text.DrawLine(text, new Vector2(LeftMargin, LeftMargin + (row++ * LineHeight)), Color.White);

    private static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _text.Dispose();
    }
}