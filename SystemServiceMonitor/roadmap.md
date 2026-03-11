# Roadmap & V2 Deferrals

## Completed in V1
- WPF Tray App interface and Dashboard.
- SQLite Persistence and Serilog structured logging.
- Support for Process, HTTP, Windows Service, WSL, and Docker monitoring targets.
- Retry, backoff, and quarantine repair lifecycle.
- Local LLM diagnosis request.
- GitHub Commit detection.

## Deferred to V2
- Execution of arbitrary AI recommended commands (too risky without an allowlist policy system).
- Distributed multi-node orchestration (out of scope for single workstation sysadmin focus).
- Deep integration with package managers (e.g., winget updates).
- Dependency-aware startup graph algorithm (V1 processes independently based on naive state).
- Configurable webhooks or external alerting tools (focusing on in-app UI for V1).
