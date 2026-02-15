using System.Reflection;

namespace PhotoBooth.Server;

public static class DefaultSettingsRestorer
{
    private const string EmbeddedResourceName = "appsettings.default.json";

    public static void EnsureSettingsExist(string directory, Assembly? assembly = null)
    {
        var settingsPath = Path.Combine(directory, "appsettings.json");

        if (File.Exists(settingsPath))
            return;

        assembly ??= Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found in assembly '{assembly.FullName}'.");

        using var fileStream = File.Create(settingsPath);
        stream.CopyTo(fileStream);

        Serilog.Log.Information("Restored default appsettings.json to {Path}", settingsPath);
    }
}
