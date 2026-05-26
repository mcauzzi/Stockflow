using Stockflow.Simulation.Core;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Modules;

namespace Stockflow.Simulation.Component;

// Cosa sa fare un componente nella simulazione
public interface ISimComponent
{
    int                             Id       { get; }
    GridCoord                       Position { get; }
    Direction                       Facing   { get; }
    ComponentType                   Type     { get; }
    IReadOnlyList<IComponentModule> Modules  { get; }
    SimEntity?                      Occupant { get; }
    // Porte attraverso cui le entità entrano/escono
    IReadOnlyList<Port> Ports { get; }
    // Events generated during Tick — drained by SimulationEngine
    IReadOnlyList<SimulationEvent> PendingEvents { get; }

    // --- Schema-driven configuration (Phase 1D) ---

    /// <summary>
    /// Declares all configurable (and read-only metric) properties for this component type.
    /// Used by the engine for validation and by clients for dynamic UI generation.
    /// The list is static per component type — it does not change at runtime.
    /// </summary>
    IReadOnlyList<PropertySchema> ConfigSchema { get; }

    /// <summary>
    /// Applies validated property values from a ConfigureComponentCommand.
    /// Only writable properties (IsReadOnly == false) in the schema should be accepted.
    /// Returns null on success, or an error message on failure.
    /// </summary>
    string? ApplyConfig(IReadOnlyDictionary<string, string> properties);

    /// <summary>
    /// Exports the current values of ALL properties (writable + read-only metrics)
    /// as a string dictionary, matching the keys in ConfigSchema.
    /// Used by ComponentSerializer to build ComponentState.Properties for the wire.
    /// </summary>
    Dictionary<string, string> ExportProperties();

    // Chiamato ogni tick — gestisce logica interna
    // (es. il traslo muove le forche, l'accumulo decide se rilasciare)
    void Tick(float deltaTime);

    // Un'entità arriva a una porta
    bool TryAccept(SimEntity entity, PortId fromPort);
}
