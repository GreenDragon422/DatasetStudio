# Avalonia Headless Tests Package

This package creates a reusable NUnit-based Avalonia headless test project inside another repository.

## What it installs

- A headless test project wired with `Avalonia.Headless.NUnit`
- A minimal `Application` bootstrapper for headless rendering
- Reusable PNG and GIF capture helpers
- A smoke test that proves rendering works

## Quick start

1. Extract this package anywhere inside the target repository.
2. Run one of the installers:

```bash
./install.sh --target ./src/MyApp/MyApp.csproj
```

```powershell
./install.ps1 -TargetProjectPath .\src\MyApp\MyApp.csproj
```

3. Run the generated tests:

```bash
dotnet test ./MyApp.HeadlessTests/MyApp.HeadlessTests.csproj
```

## Installer behavior

- Detects the target project's `TargetFramework` or first `TargetFrameworks` entry
- Reuses the target project's Avalonia package version when possible
- Creates a sibling test project by default, or `tests/<Project>.HeadlessTests` when the app project sits at the repo root
- Adds the new test project to the nearest `.sln` or `.slnx` if one is found
- Adds an exclusion block to the target app project if the generated test folder would otherwise be compiled by SDK default globs

## Optional arguments

`install.sh`

- `--target`: required path to the app `.csproj`
- `--test-project-name`: optional override for the generated test project name
- `--test-dir`: optional output directory for the generated test project
- `--solution`: optional explicit `.sln` or `.slnx` path

`install.ps1`

- `-TargetProjectPath`: required path to the app `.csproj`
- `-TestProjectName`: optional override for the generated test project name
- `-TestProjectDirectory`: optional output directory for the generated test project
- `-SolutionPath`: optional explicit `.sln` or `.slnx` path

## Generated output

The generated project writes screenshots and GIFs into `TestOutputs/` under the test project folder.
