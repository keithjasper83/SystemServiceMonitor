# Architecture

The system consists of several projects:

1. **SystemServiceMonitor.Core**: Contains core domain models, enums, EF Core database context, and shared business logic.
2. **SystemServiceMonitor.Wpf**: The WPF Windows desktop application acting as the UI, tray application, and host.
3. **SystemServiceMonitor.Cli**: A console application for running EF Core migrations without WPF dependencies.
4. **SystemServiceMonitor.Tests**: XUnit test project.

## Database

SQLite is used for persistence. Migrations are stored in `SystemServiceMonitor.Core/Migrations` and run from the CLI project.

## Logging

Structured logging is implemented using Serilog, writing to console and rolling files.

## Monitored Targets

Resources such as Windows Services, Processes, HTTP endpoints, WSL, and Docker workloads can be added to the application, maintaining their state and reacting with automatic restart strategies upon failures.
