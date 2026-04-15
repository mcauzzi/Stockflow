namespace Stockflow.Simulation;

public abstract class Entity
{
    public required EntityState State { get; set; }
    public abstract void        Simulate();
}