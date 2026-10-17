using XTouch.Abstractions;
using XTouch.Abstractions.Mcu;
using XTouch.Contracts;
using XTouch.Core.Engine;

namespace XTouch.Service.Api;

public static class DtoMapper
{
    public static SurfaceDto ToDto(SurfaceSnapshot s, EngineStatus status)
    {
        var strips = new StripDto[McuConstants.Strips];
        for (int i = 0; i < strips.Length; i++)
        {
            var info = i < status.Strips.Count ? status.Strips[i] : null;
            strips[i] = new StripDto(
                i, info?.ChannelIndex, info?.ChannelKey ?? "",
                s.Faders[i], s.FaderTouched[i],
                s.LcdTop[i], s.LcdBottom[i], s.LcdColors[i].ToString(),
                s.Rings[i].Position, s.Rings[i].Mode.ToString(), s.Rings[i].Center,
                s.Leds[McuConstants.RecBase + i] != LedState.Off,
                s.Leds[McuConstants.SoloBase + i] != LedState.Off,
                s.Leds[McuConstants.MuteBase + i] != LedState.Off,
                s.Leds[McuConstants.SelectBase + i] != LedState.Off,
                s.Meters[i]);
        }
        return new SurfaceDto(strips, s.Faders[McuConstants.MasterFader], s.FaderTouched[McuConstants.MasterFader],
            s.Display, s.Leds.Select(l => (int)l).ToArray());
    }

    public static StatusDto ToDto(EngineStatus s, ITargetRegistry targets, string transport, bool traceEnabled) => new(
        ApiInfo.Version,
        s.SurfaceConnected, s.SurfacePort, transport,
        targets.All.Select(t => new TargetStatusDto(t.Id, t.DisplayName, s.Targets.TryGetValue(t.Id, out var c) && c)).ToList(),
        s.ProfileId, s.ProfileName, s.BankOffset, s.ChannelCount, s.Layer,
        s.Layers.Select(l => new LayerDto(l.Id, l.Label)).ToList(),
        s.ShowEncoderValues, traceEnabled);
}
