using Stockflow.Simulation.Entity;

namespace Stockflow.Simulation.Modules;

public interface IComponentModule
{
    void OnEntityEnter(SimEntity entity);
    void OnEntityExit(SimEntity  entity);
    void OnTick(float            deltaTime);
}
