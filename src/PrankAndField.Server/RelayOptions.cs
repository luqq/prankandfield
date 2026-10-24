namespace PrankAndField.Server;

/// <summary>Settings of the relay, bound from the "Relay" configuration section.</summary>
public sealed class RelayOptions
{
    /// <summary>Browser origins allowed to open the hub (the Pages site and the local dev server).</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Input delay in ticks handed to both peers when a match starts.</summary>
    public int InputDelay { get; set; } = 4;
}
