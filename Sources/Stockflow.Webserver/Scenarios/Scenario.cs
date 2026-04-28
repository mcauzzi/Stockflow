namespace Stockflow.Webserver.Scenarios;

/// <summary>
/// Schema scenario file-based, mirror del formato §6.1 di ARCHITECTURE_StockFlow_v0.3.
/// I campi non ancora supportati dalla simulazione (Budget, SkuCatalog, OrderProfile,
/// Objectives, AvailableComponents, Duration) sono pass-through: persistiti ma
/// ignorati dal loader con warning a log.
/// </summary>
public sealed record Scenario
{
    public required string Id          { get; init; }
    public required string Name        { get; init; }
    public          string? Description { get; init; }

    public GridSize?           GridSize            { get; init; }
    public int?                Budget              { get; init; }
    public IReadOnlyList<string>? AvailableComponents { get; init; }
    public IReadOnlyList<PreplacedComponent>? PreplacedComponents { get; init; }
    public SkuCatalog?         SkuCatalog          { get; init; }
    public OrderProfile?       OrderProfile        { get; init; }
    public IReadOnlyDictionary<string, Objective>? Objectives { get; init; }
    public int?                Duration            { get; init; }
}

public sealed record GridSize(int Width, int Height);

public sealed record PreplacedComponent(string Type, int[] Position, string Direction);

public sealed record SkuCatalog(int Count, IReadOnlyDictionary<string, SkuClass> Classes);

public sealed record SkuClass(int Percentage, string AccessFrequency);

public sealed record OrderProfile(
    int            BaseRate,
    int            PeakRate,
    int            PeakStartTime,
    int            PeakDuration,
    LinesPerOrder? LinesPerOrder,
    int            Deadline);

public sealed record LinesPerOrder(int Min, int Max);

public sealed record Objective(string Type, double? Value = null);
