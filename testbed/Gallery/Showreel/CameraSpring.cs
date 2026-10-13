// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss
using Godot;
namespace testbed;

/// <summary>Moves a camera after a keyed lens, and turns it toward a point that follows the rig's target, each on a
/// critically damped spring: every move starts and settles softly, and at rest the camera frames the shot exactly. A
/// jump, such as the loop's restart, moves it at once.</summary>
public partial class CameraSpring : Node
{
    // Roughly the seconds the camera trails its keys by.
    private const double SmoothTime = 0.5;
    // Farther than the lens moves in a frame, short of a jump.
    private const float Jump = 2;

    public Camera3D Camera { get; init; } = null!;
    public Node3D Lens { get; init; } = null!;
    public Node3D Target { get; init; } = null!;

    private Vector3 position, velocity, aim, turning, last;
    private bool placed;

    public CameraSpring()
    {
        Name = nameof(CameraSpring);
        // After both tween runners, so the camera follows this frame's keys.
        ProcessPriority = 1001;
    }

    public override void _Process(double delta) => Follow(delta);

    /// <summary>Moves the camera <paramref name="delta"/> seconds closer to its keys.</summary>
    public void Follow(double delta)
    {
        var goal = Lens.GlobalPosition;
        if (!placed || goal.DistanceTo(last) > Jump)
        {
            (position, velocity, aim, turning, placed) = (goal, Vector3.Zero, Target.GlobalPosition, Vector3.Zero, true);
        }
        else
        {
            position = Damp(position, goal, ref velocity, delta);
            aim = Damp(aim, Target.GlobalPosition, ref turning, delta);
        }
        last = goal;
        Camera.GlobalPosition = position;
        Camera.LookAt(aim, Lens.GlobalBasis.Y);
    }

    /// <summary>Unity's SmoothDamp: an exact step of a critically damped spring for any delta.</summary>
    private static Vector3 Damp(Vector3 current, Vector3 goal, ref Vector3 velocity, double delta)
    {
        var omega = 2 / SmoothTime;
        var x = omega * delta;
        var decay = (float)(1 / (1 + x + 0.48 * x * x + 0.235 * x * x * x));
        var change = current - goal;
        var step = (velocity + change * (float)omega) * (float)delta;
        velocity = (velocity - step * (float)omega) * decay;
        return goal + (change + step) * decay;
    }
}
