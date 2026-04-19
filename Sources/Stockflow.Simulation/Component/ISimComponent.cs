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

    // Chiamato ogni tick — gestisce logica interna
    // (es. il traslo muove le forche, l'accumulo decide se rilasciare)
    void Tick(float deltaTime);

    // Un'entità arriva a una porta
    bool TryAccept(SimEntity entity, PortId fromPort);
}
