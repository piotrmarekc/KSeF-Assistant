# KSeF Assistant

Aplikacja Windows do pobierania faktur zakupowych z Krajowego Systemu e-Fakturowania (KSeF), filtrowania po NIP dostawców, eksportu do PDF i generowania raportów Excel.

## Wymagania

| Wymaganie | Wersja |
|-----------|--------|
| Windows | 10 (1809) / 11 |
| .NET SDK | 9.0+ |
| Windows App SDK | 1.6+ |
| Visual Studio | 2022 (17.x) z workloadem **Windows application development** |

### Instalacja prerekvizytów

1. **[.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)**
2. **[Windows App SDK 1.6](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)** (runtime + VSIX)
3. W Visual Studio: `Tools → Get Tools and Features → Windows application development`

## Pierwsze uruchomienie

```bash
# Klonowanie / otwarcie folderu
cd "c:\Users\Piotr\Claude\KSEF Assistant"

# Przywracanie pakietów NuGet
dotnet restore KSeFAssistant.sln

# Kompilacja
dotnet build KSeFAssistant.sln -c Debug

# Uruchomienie (wymaga x64)
dotnet run --project KSeFAssistant.UI -r win-x64
```

Lub otwórz `KSeFAssistant.sln` w **Visual Studio 2022** i naciśnij `F5`.

## Konfiguracja autoryzacji KSeF

### Tok API (do 31.12.2026)

1. Zaloguj się do **[Modułu Certyfikatów i Uprawnień (MCU)](https://ksef.podatki.gov.pl)**
2. Wygeneruj token API (sekcja `Tokeny`)
3. W aplikacji przejdź do **Ustawienia** i wklej token

### Certyfikat X.509 (obowiązkowy od 01.01.2027)

1. W MCU złóż wniosek o certyfikat → pobierz plik `.pfx`
2. W aplikacji: Ustawienia → wybierz plik `.pfx` → podaj hasło

### Środowisko testowe

Przed podłączeniem do produkcji przetestuj na środowisku testowym:
- URL: `https://api-test.ksef.mf.gov.pl`
- Testowe podmioty: POST `/testdata/subject`
- Testowe uprawnienia: POST `/testdata/permissions`

## Bezpieczeństwo danych

- Token API i hasło certyfikatu szyfrowane przez **DPAPI** (Windows Data Protection API)
- Klucz szyfrowania oparty na koncie Windows — dane niedostępne dla innych użytkowników
- Logi w `%LOCALAPPDATA%\KSeFAssistant\logs\` — **nie zawierają** tokenów ani danych uwierzytelniających

## Uruchomienie testów

```bash
dotnet test KSeFAssistant.Tests --logger "console;verbosity=normal"
```

Testy integracyjne używają **WireMock.Net** — nie wymagają połączenia z KSeF.

## Struktura projektu

```
KSeFAssistant.sln
├── KSeFAssistant.Core/           # Modele, interfejsy, serwisy (PDF, Excel, filtrowanie)
├── KSeFAssistant.Infrastructure/ # Klient KSeF (Refit), DPAPI, DTO
├── KSeFAssistant.UI/             # WinUI 3, MVVM (ViewModels, Pages)
└── KSeFAssistant.Tests/          # xUnit + WireMock.Net
```

## Kluczowe pliki

| Plik | Opis |
|------|------|
| [KSeFAssistant.Infrastructure/KSeF/IKSeFApi.cs](KSeFAssistant.Infrastructure/KSeF/IKSeFApi.cs) | Refit interface — endpointy KSeF API |
| [KSeFAssistant.Infrastructure/KSeF/KSeFService.cs](KSeFAssistant.Infrastructure/KSeF/KSeFService.cs) | Główna logika auth + pobierania |
| [KSeFAssistant.Infrastructure/Security/WindowsCredentialManager.cs](KSeFAssistant.Infrastructure/Security/WindowsCredentialManager.cs) | DPAPI credential storage |
| [KSeFAssistant.Core/Services/PdfExportService.cs](KSeFAssistant.Core/Services/PdfExportService.cs) | Generowanie PDF (QuestPDF) |
| [KSeFAssistant.Core/Services/ExcelReportService.cs](KSeFAssistant.Core/Services/ExcelReportService.cs) | Raport Excel (ClosedXML) |
| [KSeFAssistant.UI/ViewModels/InvoiceListViewModel.cs](KSeFAssistant.UI/ViewModels/InvoiceListViewModel.cs) | Główny ViewModel — faktury |

## Znane ograniczenia / TODO

- [ ] Autoryzacja certyfikatem X.509 (`KSeFService.AuthenticateWithCertificateAsync`) — stub, wymaga implementacji podpisywania XML
- [ ] Pakowanie MSIX — dodaj `Package.appxmanifest` do projektu UI
- [ ] Aktualizacje aplikacji — sprawdzanie nowej wersji przy starcie

## Dokumentacja API

- [KSeF API v2 — interaktywna dokumentacja](https://api.ksef.mf.gov.pl/docs/v2)
- [KSeF API v2 — OpenAPI JSON](https://api.ksef.mf.gov.pl/docs/v2/openapi.json)
- [Wsparcie dla integratorów](https://ksef.podatki.gov.pl/ksef-na-okres-obligatoryjny/wsparcie-dla-integratorow)
