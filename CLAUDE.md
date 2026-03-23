# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build entire solution (must specify Platform for WinUI project)
dotnet build KSeFAssistant.sln -c Debug /p:Platform=x64

# Build only non-UI projects (Core, Infrastructure, Tests) — no Platform required
dotnet build KSeFAssistant.Core
dotnet build KSeFAssistant.Infrastructure

# Run all tests
dotnet test KSeFAssistant.Tests --logger "console;verbosity=normal"

# Run a single test class
dotnet test KSeFAssistant.Tests --filter "FullyQualifiedName~InvoiceFilterServiceTests"

# Run the app (requires win-x64 runtime)
dotnet run --project KSeFAssistant.UI -r win-x64
```

## Known Build Issue: XamlCompiler.exe

The WinUI 3 project (`KSeFAssistant.UI`) has a known issue where `XamlCompiler.exe` (net472 tool from Windows App SDK) silently exits with code 1 during `dotnet build`. The root cause traced via IL analysis: `GetIXamlType("Microsoft.UI.Xaml.Markup.IXamlType")` returns null → `LogError_CannotResolveWinUIMetadata()` → `DoExecute()` returns false without writing `output.json`.

Workaround in csproj: `_ForceXamlDllCompiler` target sets `UseXamlCompilerExecutable=false` to use the DLL-based compiler (net6.0). This avoids the exe path but may fail on newer MSBuild versions with "Type must be a type provided by the runtime". The UI project builds correctly in Visual Studio 2022. For CLI builds, use VS Developer PowerShell or `msbuild.exe` from the VS installation.

## Project Architecture

Four-project solution:

- **KSeFAssistant.Core** — Domain models, interfaces, business logic services (PDF/Excel export, invoice filtering). No external API dependencies.
- **KSeFAssistant.Infrastructure** — KSeF API integration (Refit), DPAPI credential storage, DTO mapping. Implements interfaces from Core.
- **KSeFAssistant.UI** — WinUI 3 (Windows App SDK 1.8), MVVM with CommunityToolkit.Mvvm. Views and ViewModels only, no business logic.
- **KSeFAssistant.Tests** — xUnit + NSubstitute + WireMock.Net. Unit tests for services, integration tests mock the KSeF HTTP API.

Dependency flow: `UI → Core ← Infrastructure`, `Tests → Core + Infrastructure`.

## Key Patterns

**KSeF API flow (in `KSeFService.cs`):**
1. `POST /online/Session/AuthorizationChallenge` → get challenge
2. `POST /online/Session/InitToken` (token auth) or `InitSigned` (cert auth) → get `sessionToken`
3. `POST /online/Query/InvoiceQuery` → get `queryReferenceNumber`
4. Poll `GET /online/Query/QueryStatus` until ready → get `numberOfParcels`
5. Fetch each parcel: `GET /online/Query/QueryResult?queryPartNumber={n}`
6. Per-invoice XML: `GET /invoices/ksef/{ksefNumber}` → parse FA_v3 → `InvoiceRecord`
7. `DELETE /auth/sessions/current` on logout

**Invoice streaming:** `GetPurchaseInvoicesAsync` returns `IAsyncEnumerable<InvoiceRecord>` — results are yielded parcel-by-parcel. The ViewModel adds them to `ObservableCollection` as they arrive.

**Credential storage:** `WindowsCredentialManager` uses `ProtectedData.Protect()` (DPAPI, `CurrentUser` scope) and stores encrypted bytes to `%LOCALAPPDATA%\KSeFAssistant\credentials.enc`. Never persist `sessionToken` — it lives in memory only (`SessionContext`).

**DI registration:** `AddInfrastructure(services, environment)` in `KSeFAssistant.Infrastructure/DependencyInjection.cs` registers all services including Refit client with resilience pipeline. Call it from `App.xaml.cs`.

## Auth Modes

Both must be supported simultaneously until 31.12.2026:
- **Token API** — Base64-encoded token generated in MCU portal, valid to 31.12.2026
- **Certificate X.509 (.pfx)** — mandatory from 01.01.2027; `AuthenticateWithCertificateAsync` is currently a stub

## KSeF Environments

| Name | Base URL |
|------|----------|
| Production | `https://api.ksef.mf.gov.pl` |
| Test | `https://api-test.ksef.mf.gov.pl` |
| Demo | `https://api-demo.ksef.mf.gov.pl` |

Selected via `KSeFEnvironment` enum in `SessionContext`.
