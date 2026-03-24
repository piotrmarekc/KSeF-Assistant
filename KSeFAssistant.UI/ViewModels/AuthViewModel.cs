using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSeFAssistant.Core.Interfaces;
using KSeFAssistant.Core.Models;
using Microsoft.Extensions.Logging;

namespace KSeFAssistant.UI.ViewModels;

public sealed partial class AuthViewModel : ObservableObject
{
    private readonly ICredentialManager _credentials;
    private readonly IKSeFService _ksefService;
    private readonly ILogger<AuthViewModel> _logger;

    private string _nip = string.Empty;
    public string Nip { get => _nip; set => SetProperty(ref _nip, value); }

    private string _apiToken = string.Empty;
    public string ApiToken { get => _apiToken; set => SetProperty(ref _apiToken, value); }

    private string _certificatePath = string.Empty;
    public string CertificatePath { get => _certificatePath; set => SetProperty(ref _certificatePath, value); }

    private string _certificatePassword = string.Empty;
    public string CertificatePassword { get => _certificatePassword; set => SetProperty(ref _certificatePassword, value); }

    private KSeFEnvironment _selectedEnvironment = KSeFEnvironment.Test;
    public KSeFEnvironment SelectedEnvironment { get => _selectedEnvironment; set => SetProperty(ref _selectedEnvironment, value); }

    private AuthMethod _selectedAuthMethod = AuthMethod.Token;
    public AuthMethod SelectedAuthMethod
    {
        get => _selectedAuthMethod;
        set
        {
            if (SetProperty(ref _selectedAuthMethod, value))
                IsTokenMode = value == AuthMethod.Token;
        }
    }

    private bool _isTokenMode = true;
    public bool IsTokenMode { get => _isTokenMode; set => SetProperty(ref _isTokenMode, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private bool _isStatusSuccess;
    public bool IsStatusSuccess { get => _isStatusSuccess; set => SetProperty(ref _isStatusSuccess, value); }

    public IReadOnlyList<KSeFEnvironment> Environments { get; } =
        [KSeFEnvironment.Test, KSeFEnvironment.Demo, KSeFEnvironment.Production];

    public IReadOnlyList<AuthMethod> AuthMethods { get; } =
        [AuthMethod.Token, AuthMethod.Certificate];

    public IAsyncRelayCommand TestConnectionCommand { get; }

    public AuthViewModel(ICredentialManager credentials, IKSeFService ksefService,
        ILogger<AuthViewModel> logger)
    {
        _credentials = credentials;
        _ksefService = ksefService;
        _logger = logger;

        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, CanTestConnection);

        LoadSavedSettings();
    }

    private void LoadSavedSettings()
    {
        Nip = _credentials.LoadNip() ?? string.Empty;
        SelectedEnvironment = _credentials.LoadEnvironment();
        SelectedAuthMethod = _credentials.LoadAuthMethod();
        CertificatePath = _credentials.LoadCertificatePath() ?? string.Empty;
        IsTokenMode = SelectedAuthMethod == AuthMethod.Token;
        // Tokenów nie ładujemy do pola tekstowego ze względów bezpieczeństwa
    }

    [RelayCommand]
    private void SaveSettings()
    {
        if (string.IsNullOrWhiteSpace(Nip) || Nip.Length != 10 || !Nip.All(char.IsDigit))
        {
            SetStatus("NIP musi składać się dokładnie z 10 cyfr.", false);
            return;
        }

        _credentials.SaveNip(Nip.Trim());
        _credentials.SaveEnvironment(SelectedEnvironment);
        _credentials.SaveAuthMethod(SelectedAuthMethod);

        if (SelectedAuthMethod == AuthMethod.Token && !string.IsNullOrWhiteSpace(ApiToken))
        {
            _credentials.SaveApiToken(ApiToken.Trim());
            ApiToken = string.Empty; // wyczyść pole po zapisaniu
        }

        if (SelectedAuthMethod == AuthMethod.Certificate)
        {
            if (!string.IsNullOrWhiteSpace(CertificatePath))
                _credentials.SaveCertificatePath(CertificatePath);
            if (!string.IsNullOrWhiteSpace(CertificatePassword))
            {
                _credentials.SaveCertificatePassword(CertificatePassword);
                CertificatePassword = string.Empty;
            }
        }

        SetStatus("Ustawienia zostały zapisane.", true);
        _logger.LogInformation("Ustawienia uwierzytelnienia zapisane (NIP: {Nip}, Środowisko: {Env})",
            Nip, SelectedEnvironment);
        TestConnectionCommand.NotifyCanExecuteChanged();
    }

    private async Task TestConnectionAsync(CancellationToken ct)
    {
        IsBusy = true;
        StatusMessage = "Testowanie połączenia...";
        IsStatusSuccess = false;

        try
        {
            var nip = _credentials.LoadNip() ?? Nip;
            var env = SelectedEnvironment;

            SessionContext session;
            if (SelectedAuthMethod == AuthMethod.Token)
            {
                var token = _credentials.LoadApiToken();
                if (string.IsNullOrEmpty(token))
                {
                    SetStatus("Najpierw zapisz token API.", false);
                    return;
                }
                session = await _ksefService.AuthenticateWithTokenAsync(nip, token, env, ct);
            }
            else
            {
                var pfxPath = _credentials.LoadCertificatePath();
                var pfxPass = _credentials.LoadCertificatePassword();
                session = await _ksefService.AuthenticateWithCertificateAsync(
                    nip, pfxPath!, pfxPass!, env, ct);
            }

            await _ksefService.LogoutAsync(session, ct);
            SetStatus($"Połączenie z KSeF ({env}) udane! Sesja wygasała o {session.ExpiresAt:HH:mm:ss}.", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd testowania połączenia KSeF");
            SetStatus($"Błąd połączenia: {ex.Message}", false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanTestConnection() => !IsBusy && _credentials.HasValidCredentials();

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
    }
}
