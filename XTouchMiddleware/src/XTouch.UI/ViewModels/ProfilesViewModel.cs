using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XTouch.Contracts;
using XTouch.UI.Services;

namespace XTouch.UI.ViewModels;

/// <summary>List, edit (JSON), validate, save, activate and duplicate profiles. Validation is done by the service.</summary>
public sealed partial class ProfilesViewModel(IServiceClient client) : ObservableObject
{
    public ObservableCollection<ProfileSummaryDto> Profiles { get; } = [];
    public ObservableCollection<string> Errors { get; } = [];

    [ObservableProperty] private ProfileSummaryDto? _selected;
    [ObservableProperty] private string _editorText = "";
    [ObservableProperty] private string _loadedText = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _newId = "";
    [ObservableProperty] private bool _isBusy;

    public bool IsDirty => EditorText != LoadedText;
    partial void OnEditorTextChanged(string value) => OnPropertyChanged(nameof(IsDirty));
    partial void OnLoadedTextChanged(string value) => OnPropertyChanged(nameof(IsDirty));

    partial void OnSelectedChanged(ProfileSummaryDto? value)
    {
        if (value is not null) _ = LoadSelected(value.Id);
    }

    [RelayCommand]
    public async Task Refresh()
    {
        if (!client.IsConnected) return;
        await Run(async () =>
        {
            var keep = Selected?.Id;
            var list = await client.GetProfilesAsync();
            Profiles.Clear();
            foreach (var p in list) Profiles.Add(p);
            Selected = Profiles.FirstOrDefault(p => p.Id == keep) ?? Profiles.FirstOrDefault(p => p.IsActive) ?? Profiles.FirstOrDefault();
        });
    }

    private async Task LoadSelected(string id)
    {
        await Run(async () =>
        {
            var json = await client.GetProfileJsonAsync(id) ?? "";
            LoadedText = EditorText = json;
            Errors.Clear();
            Message = $"Loaded '{id}'.";
        });
    }

    [RelayCommand]
    private Task Validate() => Run(async () => Show(await client.ValidateProfileAsync(EditorText), "Profile is valid."));

    [RelayCommand]
    private Task Save() => Run(async () =>
    {
        if (Selected is null) return;
        var result = await client.SaveProfileAsync(Selected.Id, EditorText);
        Show(result, Selected.IsActive ? $"Saved '{Selected.Id}' — applied to the surface." : $"Saved '{Selected.Id}'.");
        if (result.Ok) LoadedText = EditorText;
    });

    [RelayCommand]
    private Task Revert() => Selected is null ? Task.CompletedTask : LoadSelected(Selected.Id);

    [RelayCommand]
    private Task Activate() => Run(async () =>
    {
        if (Selected is null) return;
        var id = Selected.Id;
        if (Show(await client.ActivateProfileAsync(id), $"Activated '{id}'.")) await Refresh();
    });

    [RelayCommand]
    private Task Delete() => Run(async () =>
    {
        if (Selected is null) return;
        var id = Selected.Id;
        if (Show(await client.DeleteProfileAsync(id), $"Deleted '{id}'.")) { Selected = null; await Refresh(); }
    });

    /// <summary>Saves the editor content as a new profile with id <see cref="NewId"/>.</summary>
    [RelayCommand]
    private Task Duplicate() => Run(async () =>
    {
        var id = NewId.Trim();
        if (id.Length == 0) { Show(new ValidationResultDto(false, ["Enter an id for the new profile."]), ""); return; }
        JsonNode? node;
        try { node = JsonNode.Parse(EditorText, documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip }); }
        catch (System.Text.Json.JsonException ex) { Show(new ValidationResultDto(false, [ex.Message]), ""); return; }
        if (node is not JsonObject obj) { Show(new ValidationResultDto(false, ["Profile must be a JSON object."]), ""); return; }
        obj["id"] = id;
        obj["name"] = $"{obj["name"]?.GetValue<string>() ?? id} (copy)";
        if (Show(await client.SaveProfileAsync(id, obj.ToJsonString(new() { WriteIndented = true })), $"Created '{id}'."))
        {
            NewId = "";
            await Refresh();
            Selected = Profiles.FirstOrDefault(p => p.Id == id);
        }
    });

    private bool Show(ValidationResultDto result, string okMessage)
    {
        Errors.Clear();
        foreach (var e in result.Errors) Errors.Add(e);
        Message = result.Ok ? okMessage : $"{result.Errors.Count} problem(s) — see below.";
        return result.Ok;
    }

    private async Task Run(Func<Task> action)
    {
        IsBusy = true;
        try { await action(); }
        catch (Exception ex) { Message = ex.Message; }
        finally { IsBusy = false; }
    }
}
