using Stockflow.Simulation.Entity;

namespace Stockflow.Simulation.Modules;

public interface IComponentModule
{
    void OnEntityEnter(ISimEntity entity);
    void OnEntityExit(ISimEntity  entity);
    void OnTick(float         deltaTime);
}