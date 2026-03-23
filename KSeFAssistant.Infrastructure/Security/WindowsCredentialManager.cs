using KSeFAssistant.Core.Interfaces;
using KSeFAssistant.Core.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KSeFAssistant.Infrastructure.Security;

/// <summary>
/// Przechowuje konfigurację i dane uwierzytelniające szyfrowane DPAPI (CurrentUser).
/// Sekrety nigdy nie są zapisywane w plain text.
/// Ścieżka: %LOCALAPPDATA%\KSeFAssistant\
/// </summary>
public sealed class WindowsCredentialManager : ICredentialManager
{
    private readonly ILogger<WindowsCredentialManager> _logger;
    private readonly string _configDir;
    private readonly string _configPath;   // jawna konfiguracja (nip, środowisko, metoda)
    private readonly string _secretsPath;  // zaszyfrowane sekrety (DPAPI)

    private static readonly DataProtectionScope Scope = DataProtectionScope.CurrentUser;

    public WindowsCredentialManager(ILogger<WindowsCredentialManager> logger)
    {
        _logger = logger;
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KSeFAssistant");
        _configPath = Path.Combine(_configDir, "config.json");
        _secretsPath = Path.Combine(_configDir, "secrets.enc");

        Directory.CreateDirectory(_configDir);
    }

    // =====================================================================
    //  KONFIGURACJA JAWNA (niesekretna)
    // =====================================================================

    public void SaveEnvironment(KSeFEnvironment environment) =>
        UpdateConfig(cfg => cfg with { Environment = environment.ToString() });

    public KSeFEnvironment LoadEnvironment()
    {
        var cfg = ReadConfig();
        return Enum.TryParse<KSeFEnvironment>(cfg.Environment, out var env) ? env : KSeFEnvironment.Test;
    }

    public void SaveNip(string nip) => UpdateConfig(cfg => cfg with { Nip = nip });

    public string? LoadNip() => ReadConfig().Nip;

    public void SaveAuthMethod(AuthMethod method) =>
        UpdateConfig(cfg => cfg with { AuthMethod = method.ToString() });

    public AuthMethod LoadAuthMethod()
    {
        var cfg = ReadConfig();
        return Enum.TryParse<AuthMethod>(cfg.AuthMethod, out var m) ? m : AuthMethod.Token;
    }

    public void SaveCertificatePath(string pfxPath) =>
        UpdateConfig(cfg => cfg with { CertificatePath = pfxPath });

    public string? LoadCertificatePath() => ReadConfig().CertificatePath;

    // =====================================================================
    //  SEKRETY (szyfrowane DPAPI)
    // =====================================================================

    public void SaveApiToken(string token)
    {
        var secrets = ReadSecrets();
        secrets["ApiToken"] = token;
        WriteSecrets(secrets);
        _logger.LogInformation("Token API zapisany (zaszyfrowany DPAPI)");
    }

    public string? LoadApiToken()
    {
        var secrets = ReadSecrets();
        return secrets.TryGetValue("ApiToken", out var t) ? t : null;
    }

    public void DeleteApiToken()
    {
        var secrets = ReadSecrets();
        secrets.Remove("ApiToken");
        WriteSecrets(secrets);
    }

    public void SaveCertificatePassword(string password)
    {
        var secrets = ReadSecrets();
        secrets["CertPassword"] = password;
        WriteSecrets(secrets);
    }

    public string? LoadCertificatePassword()
    {
        var secrets = ReadSecrets();
        return secrets.TryGetValue("CertPassword", out var p) ? p : null;
    }

    public void DeleteCertificatePassword()
    {
        var secrets = ReadSecrets();
        secrets.Remove("CertPassword");
        WriteSecrets(secrets);
    }

    public bool HasValidCredentials()
    {
        var method = LoadAuthMethod();
        return method == AuthMethod.Token
            ? !string.IsNullOrEmpty(LoadApiToken()) && !string.IsNullOrEmpty(LoadNip())
            : !string.IsNullOrEmpty(LoadCertificatePath()) && !string.IsNullOrEmpty(LoadNip());
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    private AppConfig ReadConfig()
    {
        try
        {
            if (!File.Exists(_configPath)) return new AppConfig();
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Błąd odczytu konfiguracji");
            return new AppConfig();
        }
    }

    private void UpdateConfig(Func<AppConfig, AppConfig> update)
    {
        var cfg = update(ReadConfig());
        File.WriteAllText(_configPath, JsonSerializer.Serialize(cfg,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private Dictionary<string, string> ReadSecrets()
    {
        try
        {
            if (!File.Exists(_secretsPath)) return [];
            var encrypted = File.ReadAllBytes(_secretsPath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, Scope);
            var json = Encoding.UTF8.GetString(decrypted);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Błąd odczytu zaszyfrowanych danych (plik może być uszkodzony lub z innego konta)");
            return [];
        }
    }

    private void WriteSecrets(Dictionary<string, string> secrets)
    {
        var json = JsonSerializer.Serialize(secrets);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(plainBytes, null, Scope);
        File.WriteAllBytes(_secretsPath, encrypted);
    }

    private sealed record AppConfig
    {
        public string? Nip { get; init; }
        public string Environment { get; init; } = "Test";
        public string AuthMethod { get; init; } = "Token";
        public string? CertificatePath { get; init; }
    }
}
