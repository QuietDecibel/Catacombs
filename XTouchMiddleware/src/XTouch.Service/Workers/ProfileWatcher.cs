using XTouch.Core.Engine;
using XTouch.Core.Profiles;

namespace XTouch.Service.Workers;

/// <summary>Hot-reloads the active profile when its file is edited on disk. Invalid edits are logged and ignored.</summary>
public sealed class ProfileWatcher(ProfileStore store, EngineChannel engine, ILogger<ProfileWatcher> log) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        store.ProfileChangedOnDisk += OnChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        store.ProfileChangedOnDisk -= OnChanged;
        return Task.CompletedTask;
    }

    private void OnChanged(string id)
    {
        if (id != store.ActiveId) return;
        try
        {
            engine.Post(new ActivateProfile(store.Load(id)));
            log.LogInformation("Reloaded profile '{Id}' from disk", id);
        }
        catch (ProfileValidationException ex)
        {
            log.LogWarning("Profile '{Id}' changed on disk but is invalid; keeping the running version: {Errors}", id, string.Join("; ", ex.Errors));
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Profile '{Id}' changed on disk but could not be loaded", id);
        }
    }
}
