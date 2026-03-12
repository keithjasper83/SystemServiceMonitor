# CI/CD Closed Repair Loop Documentation

The SystemServiceMonitor repository is configured with a GitHub Actions workflow (`ci.yml`) that executes an automated test and validation pipeline on every commit or pull request.

## Workflow Execution

1. **Restore & Build**: Compiles all `.NET 8` projects, ensuring syntactic correctness and proper dependency resolution.
2. **Test**: Executes all unit tests via `dotnet test`.

## Failure Handling and Repair Context

If any step in the CI pipeline fails (such as compilation errors or failing test assertions), the CI script automatically intercepts the failure and generates a structured `repair-context.json` file.

This JSON file contains essential data for automated AI remediation, including:
- Repository metadata (Branch, Commit SHA, Workflow details).
- Recent changed files.
- The failure category.

### Jules Integration

The `repair-context.json` artifact is uploaded to the workflow run. Jules (or a compatible AI agent) should ingest this context to:
- Identify what broke.
- Create a focused, isolated commit addressing the failure without extraneous refactoring.
- Run tests locally if possible before committing.
- Push the repair commit to trigger another CI iteration.

The automation limits the loop organically by pushing discrete commits and observing the new CI run, ensuring the CI environment remains the single source of truth for build stability.
