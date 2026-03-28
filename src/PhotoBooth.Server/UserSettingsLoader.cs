namespace PhotoBooth.Server;

public static class UserSettingsLoader
{
    public static bool AddUserSettings(ConfigurationManager configuration, string directory)
    {
        var userSettingsPath = Path.Combine(directory, "appsettings.User.json");
        configuration.AddJsonFile(userSettingsPath, optional: true, reloadOnChange: true);
        return File.Exists(userSettingsPath);
    }
}
