# SystemServiceMonitor

SystemServiceMonitor is a Windows-native desktop system administration application to monitor, control, repair, and restart critical services, processes, WSL workloads, and Docker containers.

## Setup

Ensure you have .NET 8.0 SDK installed.

### Build and Run

```bash
cd SystemServiceMonitor.Wpf
dotnet build
dotnet run
```

## Keyboard Shortcuts

The application supports standard Windows accessibility shortcuts (Access Keys). Hold `Alt` and press the underlined letter to trigger an action.

*   **Alt+O:** Open Dashboard
*   **Alt+X:** Exit Application
*   **Alt+R:** Resource Type Selector
*   **Alt+F:** Filter/Path Input
*   **Alt+D:** Discover
*   **Alt+A:** Add Selected to Dashboard
*   **Alt+C:** Cancel (in forms)
*   **Alt+S:** Save (in forms)

Additionally:
*   Pressing **Enter** in the filter textbox will trigger the search automatically.
*   **Esc** can be used to cancel out of dialogs.
