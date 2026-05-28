namespace Stockflow.Simulation.Component;

/// <summary>
/// Static registry of ConfigSchema per component kind.
/// Used by the REST API to expose schemas even before any component of that type is placed.
/// When the plugin system lands (Fase 2), plugins will register their schemas here.
/// </summary>
public static class ComponentSchemaRegistry
{
    private static readonly Dictionary<string, IReadOnlyList<PropertySchema>> _schemas = new()
    {
        ["conveyor_oneway"]   = OneWayConveyor.Schema,
        ["conveyor_turn"]     = ConveyorTurn.Schema,
        ["package_generator"] = PackageGenerator.Schema,
        ["package_exit"]      = PackageExit.Schema,
        ["merge"]             = MergeLogic.Schema,
        ["diverter"]          = DiverterLogic.Schema,
    };

    public static IReadOnlyDictionary<string, IReadOnlyList<PropertySchema>> GetAll() => _schemas;

    /// <summary>
    /// Called by plugin loader at startup to register custom component schemas.
    /// </summary>
    public static void Register(string kind, IReadOnlyList<PropertySchema> schema)
        => _schemas[kind] = schema;
}
