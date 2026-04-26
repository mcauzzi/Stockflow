namespace Stockflow.Webserver.Configuration;

/// <summary>
/// Maps the wire-level <see cref="Stockflow.Protocol.Messages.SimSpeed"/> ordinal
/// (also reused as plain int by the REST endpoint) to the engine's TimeScale multiplier.
/// Single source of truth shared by REST and WebSocket entry points.
/// </summary>
public static class SpeedTable
{
    private static readonly float[] TimeScaleBySpeed = [0f, 1f, 2f, 5f, 10f, 1f];

    public const int MinSpeed = 0;
    public const int MaxSpeed = 5;

    public static float TimeScaleFor(int speed) => TimeScaleBySpeed[speed];
}
