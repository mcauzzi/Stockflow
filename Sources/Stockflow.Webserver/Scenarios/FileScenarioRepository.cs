using System.Text.Json;
using System.Text.RegularExpressions;

namespace Stockflow.Webserver.Scenarios;

public sealed partial class FileScenarioRepository : IScenarioRepository
{
    private static readonly Regex IdPattern = ScenarioIdRegex();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = true,
    };

    private readonly string _scenariosPath;
    private readonly object _lock = new();

    public FileScenarioRepository(string scenariosPath)
    {
        _scenariosPath = scenariosPath;
        Directory.CreateDirectory(_scenariosPath);
    }

    public IEnumerable<ScenarioSummary> List()
    {
        lock (_lock)
        {
            var files = Directory.EnumerateFiles(_scenariosPath, "*.json");
            var summaries = new List<ScenarioSummary>();
            foreach (var file in files)
            {
                var s = ReadFile(file);
                if (s != null)
                    summaries.Add(new ScenarioSummary(s.Id, s.Name, s.Description));
            }
            return summaries;
        }
    }

    public Scenario? Get(string id)
    {
        ValidateId(id);
        lock (_lock)
        {
            var path = PathFor(id);
            return File.Exists(path) ? ReadFile(path) : null;
        }
    }

    public void Create(Scenario scenario)
    {
        ValidateId(scenario.Id);
        lock (_lock)
        {
            var path = PathFor(scenario.Id);
            if (File.Exists(path))
                throw new ScenarioAlreadyExistsException(scenario.Id);
            WriteFile(path, scenario);
        }
    }

    public void Update(Scenario scenario)
    {
        ValidateId(scenario.Id);
        lock (_lock)
        {
            var path = PathFor(scenario.Id);
            if (!File.Exists(path))
                throw new ScenarioNotFoundException(scenario.Id);
            WriteFile(path, scenario);
        }
    }

    public bool Delete(string id)
    {
        ValidateId(id);
        lock (_lock)
        {
            var path = PathFor(id);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }

    private string PathFor(string id) => Path.Combine(_scenariosPath, $"{id}.json");

    private static Scenario? ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<Scenario>(stream, JsonOptions);
    }

    private static void WriteFile(string path, Scenario scenario)
    {
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, scenario, JsonOptions);
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrEmpty(id) || !IdPattern.IsMatch(id))
            throw new InvalidScenarioIdException(id);
    }

    [GeneratedRegex("^[a-zA-Z0-9._-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ScenarioIdRegex();
}
