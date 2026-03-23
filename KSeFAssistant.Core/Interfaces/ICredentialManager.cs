using KSeFAssistant.Core.Models;

namespace KSeFAssistant.Core.Interfaces;

public interface ICredentialManager
{
    // --- Konfiguracja ogólna ---
    void SaveEnvironment(KSeFEnvironment environment);
    KSeFEnvironment LoadEnvironment();

    void SaveNip(string nip);
    string? LoadNip();

    void SaveAuthMethod(AuthMethod method);
    AuthMethod LoadAuthMethod();

    // --- Token API ---
    void SaveApiToken(string token);
    string? LoadApiToken();
    void DeleteApiToken();

    // --- Certyfikat X.509 ---
    void SaveCertificatePath(string pfxPath);
    string? LoadCertificatePath();

    void SaveCertificatePassword(string password);
    string? LoadCertificatePassword();
    void DeleteCertificatePassword();

    bool HasValidCredentials();
}
