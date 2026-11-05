# Repository Guidelines

## Project Structure & Module Organization

This checkout is currently empty: it has no source files, tests, assets, solution, project file, or Git metadata. Place application code in a clearly named source directory (for example, `src/`), tests in a matching `tests/` directory, and non-code resources under `assets/`. Keep project and test names descriptive and consistent when adding them.

## Build, Test, and Development Commands

No build or test commands are configured yet. Once a .NET solution or project is added, run commands from the repository root:

- `dotnet build <solution-or-project>` compiles the code.
- `dotnet test <solution-or-project>` runs the test suites.
- `dotnet run --project <project-path>` starts an executable project locally.

Replace the placeholders with actual paths; document any required SDK version and local configuration alongside the project setup.

## Coding Style & Naming Conventions

Follow the formatter and analyzer settings checked into the repository when available. For C#, use four spaces, PascalCase for public types and members, and camelCase for local variables and parameters. Keep namespaces aligned with the source directory structure. Avoid committing generated output such as `bin/` and `obj/`.

## Testing Guidelines

There is no test framework or coverage policy in this checkout yet. Add tests under `tests/`, name test projects after the code they cover (for example, `Feature.Tests`), and use descriptive test names that state the behavior being checked. Run `dotnet test` before submitting changes once tests are configured.

## Commit & Pull Request Guidelines

No Git history is available to establish existing commit conventions. Use short, imperative commit subjects, optionally prefixed by the area changed (for example, `api: add health endpoint`). Pull requests should explain the change and its rationale, list validation performed, link related issues, and include screenshots when they change a user interface.

## Configuration & Secrets

Keep credentials and machine-specific settings out of source control. Provide safe example configuration where needed and document required environment variables without including real secret values.
