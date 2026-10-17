using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace XTouch.Core.Profiles;

public sealed class ProfileOptions
{
    public string Directory { get; set; } = "profiles";
    /// <summary>Profile id to activate at startup when none was activated before.</summary>
    public string Active { get; set; } = "x32-default";
}

public sealed record ProfileSummary(string Id, string Name, string Target, DateTimeOffset Modified, bool IsValid);

/// <summary>Profiles stored as JSON files (one per profile) in a directory. Thread-safe.</summary>
public sealed class ProfileStore : IDisposable
{
    private const string ActiveFile = ".active";
    private readonly ILogger<ProfileStore> _log;
    private readonly Lock _gate = new();
    private readonly FileSystemWatcher? _watcher;
    private readonly Dictionary<string, DateTime> _ownWrites = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _debounce;
    private readonly HashSet<string> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);

    public string Directory { get; }

    /// <summary>Raised (debounced) when a profile file is changed on disk by something other than this store.</summary>
    public event Action<string>? ProfileChangedOnDisk;

    public ProfileStore(IOptions<ProfileOptions> options, ILogger<ProfileStore> log)
    {
        _log = log;
        Directory = Path.GetFullPath(options.Value.Directory);
        System.IO.Directory.CreateDirectory(Directory);

        if (!System.IO.Directory.EnumerateFiles(Directory, "*.json").Any())
        {
            foreach (var p in DefaultProfiles.All()) File.WriteAllText(PathFor(p.Id), p.ToJson());
            _log.LogInformation("Wrote default profiles to {Dir}", Directory);
        }
        if (ReadActiveId() is null) WriteActiveId(options.Value.Active);

        try
        {
            _watcher = new FileSystemWatcher(Directory, "*.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Renamed += OnFileEvent;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Profile hot-reload disabled (file watching unavailable)");
        }
    }

    public IReadOnlyList<ProfileSummary> List()
    {
        var list = new List<ProfileSummary>();
        foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*.json").Order())
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var modified = File.GetLastWriteTimeUtc(file);
            try
            {
                var doc = ProfileDocument.Parse(File.ReadAllText(file));
                list.Add(new ProfileSummary(id, doc.Name, doc.Target, modified, ProfileCompiler.Validate(doc).Count == 0 && doc.Id == id));
            }
            catch (Exception)
            {
                list.Add(new ProfileSummary(id, id, "?", modified, false));
            }
        }
        return list;
    }

    public string? GetJson(string id)
    {
        var path = PathFor(id);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public ProfileDocument? Get(string id) => GetJson(id) is { } json ? ProfileDocument.Parse(json) : null;

    /// <summary>Validates and compiles, then saves. Throws <see cref="ProfileValidationException"/> when invalid.</summary>
    public CompiledProfile Save(string id, ProfileDocument doc)
    {
        if (!string.Equals(doc.Id, id, StringComparison.Ordinal))
            throw new ProfileValidationException([$"id in body ('{doc.Id}') must match '{id}'"]);
        var compiled = ProfileCompiler.Compile(doc);
        var path = PathFor(id);
        lock (_gate) _ownWrites[id] = DateTime.UtcNow;
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, doc.ToJson());
        File.Move(tmp, path, overwrite: true);
        return compiled;
    }

    public bool Delete(string id)
    {
        var path = PathFor(id);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public CompiledProfile Load(string id) =>
        ProfileCompiler.Compile(Get(id) ?? throw new FileNotFoundException($"Profile '{id}' not found"));

    public string ActiveId => ReadActiveId() ?? "x32-default";

    public void SetActive(string id) => WriteActiveId(id);

    private string? ReadActiveId()
    {
        var p = Path.Combine(Directory, ActiveFile);
        return File.Exists(p) ? File.ReadAllText(p).Trim() is { Length: > 0 } s ? s : null : null;
    }

    private void WriteActiveId(string id) => File.WriteAllText(Path.Combine(Directory, ActiveFile), id);

    public string PathFor(string id)
    {
        if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id.Contains("..") || id.StartsWith('.'))
            throw new ArgumentException($"Invalid profile id '{id}'");
        return Path.Combine(Directory, id + ".json");
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (e.Name is null || e.Name.EndsWith(".tmp", StringComparison.Ordinal)) return;
        var id = Path.GetFileNameWithoutExtension(e.Name);
        lock (_gate)
        {
            if (_ownWrites.TryGetValue(id, out var t) && DateTime.UtcNow - t < TimeSpan.FromSeconds(2)) return;
            _pendingChanges.Add(id);
            _debounce ??= new Timer(_ => FlushChanges());
            _debounce.Change(TimeSpan.FromMilliseconds(250), Timeout.InfiniteTimeSpan); // editors write in several steps
        }
    }

    private void FlushChanges()
    {
        string[] ids;
        lock (_gate) { ids = [.. _pendingChanges]; _pendingChanges.Clear(); }
        foreach (var id in ids) ProfileChangedOnDisk?.Invoke(id);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
    }
}
