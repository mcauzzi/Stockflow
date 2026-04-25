using MessagePack;
using MessagePack.Resolvers;

namespace Stockflow.Protocol.Serialization;

/// <summary>
/// Centralised MessagePack configuration shared by every process that speaks the Stockflow
/// wire protocol (server, Unity client, web client, CLI).
///
/// <para>Call <see cref="Initialize"/> once during startup before serializing anything. Subsequent
/// calls are no-ops. The configured <see cref="Options"/> are also installed as
/// <see cref="MessagePackSerializer.DefaultOptions"/> so code that does not explicitly pass
/// options still picks them up.</para>
///
/// <para>Design choices:</para>
/// <list type="bullet">
///   <item><description><b>StandardResolver</b> — our DTOs use explicit <c>[Key]</c> attributes, which the
///     standard resolver handles out of the box. Do not switch to <c>ContractlessStandardResolver</c>:
///     name-based keys are fragile under refactoring and fatter on the wire.</description></item>
///   <item><description><b>LZ4 block-array compression</b> — deltas compress 2-3x with negligible CPU
///     cost; the WebSocket broadcast benefits directly. Both peers must agree, which is why
///     compression is set here and nowhere else.</description></item>
///   <item><description><b>No custom formatters (yet)</b> — add them to the <c>CompositeResolver</c>
///     below when introducing types the standard resolver cannot handle (e.g. third-party structs).</description></item>
/// </list>
/// </summary>
public static class MessagePackConfig
{
    private static readonly object _lock = new();
    private static bool _initialized;

    /// <summary>
    /// Serializer options used for every message on the Stockflow WebSocket channel.
    /// Until <see cref="Initialize"/> is called this holds the library's untouched defaults;
    /// after initialisation it carries the Stockflow resolver and LZ4 compression.
    /// </summary>
    public static MessagePackSerializerOptions Options { get; private set; }
        = MessagePackSerializerOptions.Standard;

    /// <summary>
    /// Idempotent startup hook. Safe to call from multiple entry points (server, test host, tools).
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;

            var resolver = CompositeResolver.Create(
                StandardResolver.Instance);

            Options = MessagePackSerializerOptions.Standard
                .WithResolver(resolver)
                .WithCompression(MessagePackCompression.Lz4Block);

            MessagePackSerializer.DefaultOptions = Options;
            _initialized = true;
        }
    }
}
