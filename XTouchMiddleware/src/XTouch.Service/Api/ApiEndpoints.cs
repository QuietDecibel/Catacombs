using System.Text.Json;
using Microsoft.Extensions.Options;
using XTouch.Contracts;
using XTouch.Core.Engine;
using XTouch.Core.Profiles;
using XTouch.Surface.Transport;

namespace XTouch.Service.Api;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapGet("/health", (HubStateBroadcaster s) =>
        {
            var st = s.Status;
            return Results.Ok(new { surface = st.SurfaceConnected, targets = st.Targets.ToDictionary(t => t.Id, t => t.Connected), profile = st.ProfileId });
        });

        var api = app.MapGroup("/api");

        api.MapGet("/status", (HubStateBroadcaster s) => s.Status);
        api.MapGet("/surface", (HubStateBroadcaster s) => s.Surface);

        api.MapGet("/midi/ports", () =>
        {
            var (inputs, outputs) = DryWetMidiTransport.ListPorts();
            return new MidiPortsDto(inputs, outputs);
        });

        api.MapGet("/profiles", (ProfileStore store) =>
        {
            var active = store.ActiveId;
            return store.List().Select(p => new ProfileSummaryDto(p.Id, p.Name, p.Target, p.Modified, p.IsValid, p.Id == active));
        });

        api.MapGet("/profiles/{id}", (string id, ProfileStore store) =>
            store.GetJson(id) is { } json ? Results.Text(json, "application/json") : Results.NotFound());

        api.MapPost("/profiles/validate", async (HttpRequest req) =>
        {
            var (doc, errors) = await ReadProfile(req);
            if (doc is not null) errors = [.. ProfileCompiler.Validate(doc)];
            return new ValidationResultDto(errors.Count == 0, errors);
        });

        api.MapPut("/profiles/{id}", async (string id, HttpRequest req, ProfileStore store, EngineChannel engine) =>
        {
            var (doc, errors) = await ReadProfile(req);
            if (doc is null) return Results.BadRequest(new ValidationResultDto(false, errors));
            try
            {
                var compiled = store.Save(id, doc);
                if (store.ActiveId == id) engine.Post(new ActivateProfile(compiled));
                return Results.Ok(new ValidationResultDto(true, []));
            }
            catch (ProfileValidationException ex)
            {
                return Results.BadRequest(new ValidationResultDto(false, ex.Errors));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ValidationResultDto(false, [ex.Message]));
            }
        });

        api.MapDelete("/profiles/{id}", (string id, ProfileStore store) =>
        {
            if (store.ActiveId == id) return Results.Conflict(new ValidationResultDto(false, ["Cannot delete the active profile"]));
            return store.Delete(id) ? Results.NoContent() : Results.NotFound();
        });

        api.MapPost("/profiles/{id}/activate", (string id, ProfileStore store, EngineChannel engine) =>
        {
            try
            {
                var compiled = store.Load(id);
                store.SetActive(id);
                engine.Post(new ActivateProfile(compiled));
                return Results.Accepted();
            }
            catch (FileNotFoundException) { return Results.NotFound(); }
            catch (ProfileValidationException ex) { return Results.BadRequest(new ValidationResultDto(false, ex.Errors)); }
            catch (JsonException ex) { return Results.BadRequest(new ValidationResultDto(false, [ex.Message])); }
        });

        api.MapGet("/targets", (ITargetRegistry targets) =>
            targets.All.Select(t => new TargetStatusDto(t.Id, t.DisplayName, t.IsConnected)));

        api.MapGet("/targets/{id}/catalog", (string id, ITargetRegistry targets) =>
            targets.Get(id) is { } t ? Results.Ok(t.Catalog) : Results.NotFound());

        api.MapGet("/reference", () => new
        {
            converters = XTouch.Core.Conversion.ValueConverters.Names,
            buttons = XTouch.Abstractions.Mcu.ButtonNames.All.Order(),
            ringModes = Enum.GetNames<XTouch.Abstractions.RingMode>(),
            buttonModes = Enum.GetNames<ButtonMode>(),
        });

        api.MapPost("/surface/selftest", (EngineChannel engine) =>
        {
            engine.Post(new RunSelfTest());
            return Results.Accepted();
        });
    }

    private static async Task<(ProfileDocument? Doc, List<string> Errors)> ReadProfile(HttpRequest req)
    {
        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync();
        try
        {
            return (ProfileDocument.Parse(body), []);
        }
        catch (JsonException ex)
        {
            return (null, [$"Invalid JSON: {ex.Message}"]);
        }
    }

    /// <summary>Requires "Authorization: Bearer &lt;token&gt;" (or ?access_token= for SignalR) on /api and /hub when a token is configured.</summary>
    public static IApplicationBuilder UseApiToken(this IApplicationBuilder app)
    {
        var opt = app.ApplicationServices.GetRequiredService<IOptions<ApiOptions>>().Value;
        if (string.IsNullOrEmpty(opt.Token)) return app;
        return app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path;
            if (path.StartsWithSegments("/api") || path.StartsWithSegments(ApiInfo.HubPath))
            {
                string? supplied = ctx.Request.Headers.Authorization.ToString() is { Length: > 7 } h && h.StartsWith("Bearer ", StringComparison.Ordinal)
                    ? h[7..]
                    : ctx.Request.Query["access_token"].ToString();
                if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(supplied ?? ""), System.Text.Encoding.UTF8.GetBytes(opt.Token)))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            await next();
        });
    }
}
