# Promoting a prototype to production

Checklist for the technical teammate taking a vibe-coded prototype the rest of
the way to a customer-shippable Store app.

## 1. Cloud client

- [ ] Replace `MockFrontierClient` with the real binding via
      `AppHost.ConfigureAzureOpenAI(...)` or `AppHost.ConfigureAzureFoundry(...)`.
- [ ] Move endpoint / key out of `LocalSettings` into **Windows Credential
      Manager** or an **Azure Key Vault** reference. Never check secrets into
      the repo.
- [ ] If the demo used a single-key auth, switch to **DefaultAzureCredential**
      (managed identity or developer credential) before production.

## 2. Foundry Local

- [ ] Lock the model alias to a specific Foundry Catalog version (don't ship a
      floating alias).
- [ ] Decide whether the app ships with Foundry Local as a dependency or
      bundles the WinML execution providers itself.
- [ ] Add a first-run flow that downloads the model before the user reaches
      the chat tab (avoid 60-second first-token latency).

## 3. PII / data

- [ ] Replace `PiiScanner._names` with a customer-specific list.
- [ ] Confirm `AuditLogger` location is acceptable (default is `%LocalAppData%`).
- [ ] Decide whether the audit log is sent anywhere (Microsoft Defender,
      Sentinel, App Insights) or stays purely local.

## 4. Telemetry / observability

- [ ] Add **Application Insights** SDK (or your APM of choice) and wire it
      behind the `Settings.TelemetryEnabled` toggle.
- [ ] Surface a "diagnostics export" command on the About page for support.

## 5. Packaging & Store

- [ ] Update `Package.appxmanifest`: publisher identity, display name, version,
      capabilities. Generate a real publisher CN from Partner Center.
- [ ] Replace the placeholder `Assets\*.png` icons with branded assets.
- [ ] Configure GitHub secrets for `store-release.yml` (cert, password, Partner
      Center service principal).
- [ ] Run `store-release.yml` with `submit=false` first; sanity-check the
      bundle locally with `Add-AppPackage`.
- [ ] When ready, run with `submit=true` and wire up your preferred submission
      tool (StoreBroker.NET or `microsoft/msstore-cli`).

## 6. UI hardening

- [ ] Replace placeholder logos / colours; lock in the brand theme via
      `BrandTokens` + the appropriate `Theming\Themes\*.xaml`.
- [ ] Add accessibility names (`AutomationProperties.Name`) to all clickable
      controls.
- [ ] Test high-contrast and screen-reader paths.

## 7. Quality gate

- [ ] CI is green on `main`.
- [ ] Add a smoke-test workflow that builds + runs the MSIX through `App
      Verifier` or `winappdriver`.
- [ ] Document any deviation from the harness in the per-app `README.md`.
