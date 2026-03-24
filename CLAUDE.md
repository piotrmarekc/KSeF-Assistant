# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build only non-UI projects (Core, Infrastructure, Tests) — dotnet CLI works fine
dotnet build KSeFAssistant.Core
dotnet build KSeFAssistant.Infrastructure
dotnet test KSeFAssistant.Tests --logger "console;verbosity=normal"

# Run a single test class
dotnet test KSeFAssistant.Tests --filter "FullyQualifiedName~InvoiceFilterServiceTests"
```

**Building the full solution (including UI) requires MSBuild from Visual Studio:**

```powershell
# From PowerShell — dotnet CLI fails due to XamlCompiler issue (see below)
& "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe" `
    "KSeFAssistant.sln" /p:Configuration=Debug /p:Platform=x64 /v:m
```

**Running the app after build:**

```powershell
# Launch the compiled exe directly (dotnet run fails on WinUI 3)
Start-Process "KSeFAssistant.UI\bin\x64\Debug\net9.0-windows10.0.19041.0\KSeFAssistant.UI.exe"
```

Alternatively open `KSeFAssistant.sln` in **Visual Studio 2022** and press `F5`.

## Known Build Issue: XamlCompiler

`dotnet build` on the UI project fails with two possible errors depending on MSBuild version:
- `XamlCompiler.exe` (net472): silently exits code 1 — `GetIXamlType` returns null → no `output.json`
- DLL-based compiler (net6.0) fallback via `_ForceXamlDllCompiler` / `UseXamlCompilerExecutable=false`: fails with "Type must be a type provided by the runtime" on newer MSBuild

Workaround: use VS 2022 (`F5`) or the `msbuild.exe` from the VS installation as shown above.

## Project Architecture

Four-project solution:

- **KSeFAssistant.Core** — Domain models, interfaces, business logic services (PDF/Excel export, invoice filtering). No external API dependencies.
- **KSeFAssistant.Infrastructure** — KSeF API integration (Refit), DPAPI credential storage, DTO mapping. Implements interfaces from Core.
- **KSeFAssistant.UI** — WinUI 3 (Windows App SDK 1.8), MVVM with CommunityToolkit.Mvvm. Views and ViewModels only, no business logic.
- **KSeFAssistant.Tests** — xUnit + FluentAssertions + WireMock.Net. Unit tests for services, integration tests mock the KSeF HTTP API via `WireMockServer`.

Dependency flow: `UI → Core ← Infrastructure`, `Tests → Core + Infrastructure`.

## Key Patterns

**HTTP client layering in Infrastructure:**
- `IKSeFApi` — Refit interface, maps REST endpoints 1:1. All paths start with `/v2/` (e.g. `[Get("/v2/security/public-key-certificates")]`). Base URL is `https://api.ksef.mf.gov.pl/` without path suffix.
- `KSeFApiClient` — wraps `IKSeFApi`, adds error unwrapping (`UnwrapAsync`) and exponential retry on HTTP 429 (`RetryOnRateLimitAsync`)
- `KSeFApiClientFactory` — creates `KSeFApiClient` per `KSeFEnvironment`; accepts an optional `baseUrl` override (used in integration tests to point at WireMock)
- `KSeFService` — orchestrates auth + query flow using `KSeFApiClient`; implements `IKSeFService` from Core

**KSeF API v2 auth flow (implemented in `KSeFApiClient.AuthenticateWithTokenAsync`):**
1. `GET /v2/security/public-key-certificates` → returns JSON array of `PublicKeyCertificateDto` directly (not wrapped in an object). Pick the one with `usage: ["KsefTokenEncryption"]`.
2. `POST /v2/auth/challenge` → get `{challenge, timestamp, timestampMs}`
3. Encrypt `"{apiToken}|{timestampMs}"` with RSA-OAEP SHA-256 using the X.509 public key from step 1
4. `POST /v2/auth/ksef-token` → get `{referenceNumber, authenticationToken: {token, validUntil}}`
5. Poll `GET /v2/auth/{referenceNumber}` with `Authorization: Bearer {authenticationToken}` → wait for `status.code == 200`
6. `POST /v2/auth/token/redeem` → get `{accessToken: {token, validUntil}, refreshToken: {token, validUntil}}`
7. Use `Authorization: Bearer {accessToken.token}` for all subsequent requests
8. `DELETE /v2/auth/sessions/current` on logout

**Invoice flow:**
- `POST /v2/invoices/query/metadata` with `subjectType: "Subject2"` (buyer) + date range → paginated `{hasMore, invoices: [...]}`
- `GET /v2/invoices/ksef/{ksefNumber}` → FA_v3 XML string, parsed by `KSeFDtoMapper.EnrichFromXml`
- `GetPurchaseInvoicesAsync` returns `IAsyncEnumerable<InvoiceRecord>` with header data only. The ViewModel adds them to `ObservableCollection` as they arrive. Full XML (`LoadInvoiceXmlAsync`) is fetched separately when needed for export.

**Credential storage:** `WindowsCredentialManager` uses `ProtectedData.Protect()` (DPAPI, `CurrentUser` scope), stores encrypted bytes to `%LOCALAPPDATA%\KSeFAssistant\credentials.enc`. `SessionContext.AccessToken` lives in memory only — never persisted.

**DI registration:** `services.AddInfrastructure()` (no parameters) in `KSeFAssistant.Infrastructure/DependencyInjection.cs` registers all services. Called from `App.xaml.cs`. `KSeFEnvironment` is stored in `SessionContext` at login time, not at DI setup.

**WinUI 3 / MVVM notes:**
- `App.m_window` is a static reference used by dialogs (`ExportDialog`, `AuthPage`) to get the HWND for WinRT interop
- `Window` does not support `MinWidth`/`MinHeight` in XAML — set minimum size via `AppWindow` in code-behind
- `CheckBox.TextWrapping` is not valid — use a `TextBlock` inside `CheckBox.Content` instead
- `{Binding}` on `Run.Text` doesn't work in WinUI 3 (Run is not FrameworkElement) — use a named `TextBlock` updated from code-behind instead

**CRITICAL — explicit property declarations required in ViewModels:**

Do NOT use `[ObservableProperty]` field syntax in ViewModels. The XAML compiler runs a C# intermediate compile (`MarkupCompilePass1`) with `SkipAnalyzers=True`, which blocks all source generators including CommunityToolkit.Mvvm. Any generated name (e.g. `Nip` from `_nip`) doesn't exist in this compile, causing CS0103 errors that make the XAML pipeline produce empty `.g.cs` binding files — all runtime bindings then silently fail.

Correct pattern:
```csharp
private string _nip = string.Empty;
public string Nip { get => _nip; set => SetProperty(ref _nip, value); }
```

`[RelayCommand]` is safe — commands are accessed at runtime, not during intermediate compile. However, source-generated command names (e.g. `TestConnectionCommand` from `[RelayCommand] async Task TestConnectionAsync()`) cannot be referenced in other C# code in the same project (same intermediate compile issue). If you need to call `NotifyCanExecuteChanged()` on a command, declare it as an explicit `IAsyncRelayCommand` property instead:

```csharp
public IAsyncRelayCommand TestConnectionCommand { get; }
// In constructor:
TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, CanTestConnection);
```

**CRITICAL — use `{Binding}` not `{x:Bind}` for ViewModel properties:**

All ViewModel property bindings in XAML must use `{Binding PropertyName}` (or `{Binding PropertyName, Mode=TwoWay}`), not `{x:Bind ViewModel.PropertyName}`. Set `this.DataContext = ViewModel` before `InitializeComponent()` in code-behind. `{x:Bind}` requires `.g.cs` generated code that depends on the intermediate compile — which fails (see above).

`{x:Bind}` is only safe for static/constant expressions that don't require ViewModel property access.

## Auth Modes

Both must be supported simultaneously until 31.12.2026:
- **Token API** — token from MCU portal, encrypted with RSA-OAEP SHA-256 using KSeF public key (see auth flow above)
- **Certificate X.509 (.pfx)** — mandatory from 01.01.2027; `AuthenticateWithCertificateAsync` is currently a `NotImplementedException` stub (needs signed XML via `POST /v2/auth/xades-signature`)

## KSeF Environments

| Name | Base URL |
|------|----------|
| Production | `https://api.ksef.mf.gov.pl/` |
| Test | `https://api-test.ksef.mf.gov.pl/` |
| Demo | `https://api-demo.ksef.mf.gov.pl/` |

Selected via `KSeFEnvironment` enum in `SessionContext`. All Refit paths in `IKSeFApi` include the `/v2/` segment explicitly.
