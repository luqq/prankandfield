using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Microsoft.JSInterop;

namespace PrankAndField.UI;

/// <summary>
/// Fast path for Blazor WebAssembly: [JSExport] methods that marshal the frame buffer without JSON.
/// JavaScript obtains them through getDotnetRuntime(0).getAssemblyExports("PrankAndField.UI").
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class BrowserInterop
{
    [JSExport]
    public static void Initialize(string? recordsJson, string? language) => GameHost.Instance.Initialize(recordsJson, language);

    [JSExport]
    public static double[] Frame(double dtSeconds, int p1Mask, int p2Mask) => GameHost.Instance.Frame(dtSeconds, p1Mask, p2Mask);

    [JSExport]
    public static void SetLanguage(string language) => GameHost.Instance.SetLanguage(language);

    [JSExport]
    public static string GetLanguage() => GameHost.Instance.Language;

    [JSExport]
    public static string GetRecordsJson() => GameHost.Instance.RecordsJson;

    [JSExport]
    public static string GetAudioCatalogJson() => GameHost.Instance.AudioCatalogJson;

    [JSExport]
    public static string GetVoicePhrasesJson() => GameHost.Instance.VoicePhrasesJson;

    [JSExport]
    public static string GetDebugState() => GameHost.Instance.DebugStateJson();

    [JSExport]
    public static void JumpToEvent(int kind, int round, int players) => GameHost.Instance.Game.JumpToEvent((Core.EventKind)kind, round, players);
}

/// <summary>
/// Portable path used by Blazor Hybrid hosts (desktop WebView), where JavaScript calls
/// DotNet.invokeMethodAsync("PrankAndField.UI", ...). Payloads travel as JSON.
/// </summary>
public static class HybridInterop
{
    [JSInvokable("Initialize")]
    public static void Initialize(string? recordsJson, string? language) => GameHost.Instance.Initialize(recordsJson, language);

    [JSInvokable("Frame")]
    public static double[] Frame(double dtSeconds, int p1Mask, int p2Mask) => GameHost.Instance.Frame(dtSeconds, p1Mask, p2Mask);

    [JSInvokable("SetLanguage")]
    public static void SetLanguage(string language) => GameHost.Instance.SetLanguage(language);

    [JSInvokable("GetLanguage")]
    public static string GetLanguage() => GameHost.Instance.Language;

    [JSInvokable("GetRecordsJson")]
    public static string GetRecordsJson() => GameHost.Instance.RecordsJson;

    [JSInvokable("GetAudioCatalogJson")]
    public static string GetAudioCatalogJson() => GameHost.Instance.AudioCatalogJson;

    [JSInvokable("GetVoicePhrasesJson")]
    public static string GetVoicePhrasesJson() => GameHost.Instance.VoicePhrasesJson;

    [JSInvokable("GetDebugState")]
    public static string GetDebugState() => GameHost.Instance.DebugStateJson();

    [JSInvokable("JumpToEvent")]
    public static void JumpToEvent(int kind, int round, int players) => GameHost.Instance.Game.JumpToEvent((Core.EventKind)kind, round, players);
}
