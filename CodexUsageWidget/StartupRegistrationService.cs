using Microsoft.Win32;

namespace CodexUsageWidget;

public sealed class StartupRegistrationService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexUsageWidget";

    public void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Chemin de l'executable introuvable.");
        key.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
    }
}
