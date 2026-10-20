using Photino.Blazor;
using PrankAndField.Desktop;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        var builder = PhotinoBlazorApp.CreateBuilder(args);
        builder.RootComponents.Add<App>("#app");

        builder.ConfigureMainWindow(window =>
        {
            window.SetTitle("Prank & Field")
                  .SetSize(1024, 900)
                  .SetUseOsDefaultSize(false)
                  .Center();
        });

        await using var app = builder.Build();
        return app.Run();
    }
}
