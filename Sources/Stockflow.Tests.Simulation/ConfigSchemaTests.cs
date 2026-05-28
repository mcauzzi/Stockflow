using Stockflow.Simulation.Component;
using Stockflow.Simulation.Entity;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;

namespace Stockflow.Tests.Simulation;

public class ConfigSchemaTests
{
    // ── Validation ──

    [Fact]
    public void PropertySchema_Float_Validates_InRange()
    {
        var schema = new PropertySchema("speed", "Speed", PropertyType.Float, Min: "0.01", Max: "10");
        Assert.Null(schema.Validate("1.5"));
        Assert.Null(schema.Validate("0.01"));
        Assert.Null(schema.Validate("10"));
    }

    [Fact]
    public void PropertySchema_Float_Rejects_OutOfRange()
    {
        var schema = new PropertySchema("speed", "Speed", PropertyType.Float, Min: "0.01", Max: "10");
        Assert.NotNull(schema.Validate("0"));
        Assert.NotNull(schema.Validate("11"));
        Assert.NotNull(schema.Validate("abc"));
    }

    [Fact]
    public void PropertySchema_Enum_Validates_AllowedValues()
    {
        var schema = new PropertySchema("mode", "Mode", PropertyType.Enum,
            EnumValues: ["alternating", "priority"]);
        Assert.Null(schema.Validate("alternating"));
        Assert.Null(schema.Validate("PRIORITY"));   // case-insensitive
        Assert.NotNull(schema.Validate("invalid"));
    }

    [Fact]
    public void PropertySchema_Bool_Validates()
    {
        var schema = new PropertySchema("enabled", "Enabled", PropertyType.Bool);
        Assert.Null(schema.Validate("true"));
        Assert.Null(schema.Validate("false"));
        Assert.NotNull(schema.Validate("yes"));
    }

    // ── ApplyConfig round-trip ──

    [Fact]
    public void OneWayConveyor_ApplyConfig_ChangesSpeed()
    {
        var graph = new RoutingGraph();
        var conv  = new OneWayConveyor(1, new GridCoord(5, 5), Direction.East, 1f, graph);

        var error = conv.ApplyConfig(new Dictionary<string, string> { ["speed"] = "2.5" });

        Assert.Null(error);
        Assert.Equal(2.5f, conv.Speed);
    }

    [Fact]
    public void OneWayConveyor_ApplyConfig_RejectsNegativeSpeed()
    {
        var graph = new RoutingGraph();
        var conv  = new OneWayConveyor(1, new GridCoord(5, 5), Direction.East, 1f, graph);

        var error = conv.ApplyConfig(new Dictionary<string, string> { ["speed"] = "-1" });

        Assert.NotNull(error);
        Assert.Equal(1f, conv.Speed); // unchanged
    }

    [Fact]
    public void PackageGenerator_ExportProperties_MatchesSchema()
    {
        var graph    = new RoutingGraph();
        var entities = new EntityManager();
        var gen      = new PackageGenerator(1, new GridCoord(0, 0), Direction.East,
                           spawnRate: 2.5f, sku: "TEST", weight: 1f, size: 1f,
                           graph: graph, entities: entities);

        var exported = gen.ExportProperties();
        var schema   = gen.ConfigSchema;

        foreach (var prop in schema)
            Assert.True(exported.ContainsKey(prop.Key), $"Missing key: {prop.Key}");
    }

    [Fact]
    public void PackageExit_ApplyConfig_IgnoresWritesToReadOnlyMetrics()
    {
        var entities = new EntityManager();
        var exit     = new PackageExit(1, new GridCoord(5, 5), Direction.East, entities);

        var error = exit.ApplyConfig(new Dictionary<string, string> { ["totalProcessed"] = "999" });

        Assert.Null(error);
        Assert.Equal(0, exit.TotalProcessed); // unchanged
    }

    [Fact]
    public void MergeLogic_ApplyConfig_ChangesMode()
    {
        var graph = new RoutingGraph();
        var merge = new MergeLogic(1, new GridCoord(5, 5), Direction.North,
                        MergeMode.Alternating, TurnSide.Left, 1f, graph);

        var error = merge.ApplyConfig(new Dictionary<string, string> { ["mode"] = "priority" });

        Assert.Null(error);
        Assert.Equal(MergeMode.Priority, merge.Mode);
    }

    // ── Schema invariants ──

    [Fact]
    public void AllComponents_SchemaHasUniqueKeys()
    {
        foreach (var (kind, schema) in ComponentSchemaRegistry.GetAll())
        {
            var keys = schema.Select(s => s.Key).ToList();
            Assert.True(keys.Count == keys.Distinct().Count(),
                $"Duplicate keys in schema for '{kind}'");
        }
    }

    [Fact]
    public void AllComponents_SchemaDefaultValuesPassValidation()
    {
        foreach (var (kind, schema) in ComponentSchemaRegistry.GetAll())
        {
            foreach (var prop in schema.Where(p => !p.IsReadOnly && p.DefaultValue is not null))
            {
                var error = prop.Validate(prop.DefaultValue);
                Assert.True(error is null,
                    $"Default value '{prop.DefaultValue}' fails validation for '{kind}.{prop.Key}': {error}");
            }
        }
    }
}
