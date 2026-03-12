# Security Notes & Privilege Models

The SystemServiceMonitor application controls the state of critical services on a Windows system. Certain actions, notably restarting Windows Services or using elevated processes (`runas`), require administrative privileges.

## Elevated Actions
When a `Resource` is marked with `RequiresElevation = true`, the application will instruct `Process.Start` to use the `runas` verb. This spawns a UAC prompt for the human operator or silently executes if the hosting WPF app is already running elevated.

## Best Practices
1. Run `SystemServiceMonitor.Wpf` as a least-privilege user whenever possible.
2. Limit targets to standard user processes where feasible.
3. If managing Windows Services, ensure the sysadmin has appropriate permissions via GPO to restart specific services without full local admin rights, or run the monitor elevated.

## AI Execution Safety
The application connects to a local AI (`127.0.0.1:1234`) to get diagnostics and recommended remediation actions.
- The `AiDiagnosisResponse` includes an `isSafeToAutomate` boolean.
- Automated executions of AI-recommended actions are strictly isolated and disabled by default for V1.
- All AI interactions log the prompt and response for auditing.
