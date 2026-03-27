param(
    [Parameter(Mandatory = $true)]
    [string]$TargetProjectPath,
    [string]$TestProjectName,
    [string]$TestProjectDirectory,
    [string]$SolutionPath
)

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$templateDirectory = Join-Path $scriptDirectory "template"

$resolvedTargetProjectPath = (Resolve-Path $TargetProjectPath).Path
if (-not (Test-Path $resolvedTargetProjectPath)) {
    throw "Target project not found: $resolvedTargetProjectPath"
}

[xml]$targetProjectXml = Get-Content -Path $resolvedTargetProjectPath
$targetProjectDirectory = Split-Path -Parent $resolvedTargetProjectPath
$targetProjectBaseName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedTargetProjectPath)

if ([string]::IsNullOrWhiteSpace($TestProjectName)) {
    $TestProjectName = "$targetProjectBaseName.HeadlessTests"
}

if ([string]::IsNullOrWhiteSpace($SolutionPath)) {
    $candidateSolutions = @(
        Get-ChildItem -Path $targetProjectDirectory -Filter *.sln -File -ErrorAction SilentlyContinue
        Get-ChildItem -Path $targetProjectDirectory -Filter *.slnx -File -ErrorAction SilentlyContinue
        Get-ChildItem -Path (Split-Path -Parent $targetProjectDirectory) -Filter *.sln -File -ErrorAction SilentlyContinue
        Get-ChildItem -Path (Split-Path -Parent $targetProjectDirectory) -Filter *.slnx -File -ErrorAction SilentlyContinue
    ) | Select-Object -Unique

    if ($candidateSolutions.Count -gt 0) {
        $SolutionPath = $candidateSolutions[0].FullName
    }
}

$workspaceRoot = $targetProjectDirectory
if (-not [string]::IsNullOrWhiteSpace($SolutionPath) -and (Test-Path $SolutionPath)) {
    $workspaceRoot = Split-Path -Parent $SolutionPath
}

if ([string]::IsNullOrWhiteSpace($TestProjectDirectory)) {
    if ($targetProjectDirectory -eq $workspaceRoot) {
        $TestProjectDirectory = Join-Path (Join-Path $workspaceRoot "tests") $TestProjectName
    }
    else {
        $TestProjectDirectory = Join-Path (Split-Path -Parent $targetProjectDirectory) $TestProjectName
    }
}

New-Item -ItemType Directory -Path $TestProjectDirectory -Force | Out-Null

$targetFramework = $targetProjectXml.Project.PropertyGroup.TargetFramework | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($targetFramework)) {
    $targetFrameworks = $targetProjectXml.Project.PropertyGroup.TargetFrameworks | Select-Object -First 1
    if (-not [string]::IsNullOrWhiteSpace($targetFrameworks)) {
        $targetFramework = $targetFrameworks.Split(';')[0]
    }
}

if ([string]::IsNullOrWhiteSpace($targetFramework)) {
    throw "Unable to determine TargetFramework from $resolvedTargetProjectPath"
}

$avaloniaReference = $targetProjectXml.SelectSingleNode("//PackageReference[@Include='Avalonia']")
$avaloniaVersion = $avaloniaReference.Version
if ([string]::IsNullOrWhiteSpace($avaloniaVersion)) {
    $avaloniaVersion = "11.3.12"
}

$relativeTargetProjectPath = [System.IO.Path]::GetRelativePath($TestProjectDirectory, $resolvedTargetProjectPath)
$relativeTestProjectDirectory = [System.IO.Path]::GetRelativePath($targetProjectDirectory, $TestProjectDirectory).Replace('\', '/')
$testNamespace = $TestProjectName.Replace('.', '_').Replace('-', '_')

function New-FileFromTemplate {
    param(
        [string]$TemplateName,
        [string]$DestinationName
    )

    $content = Get-Content -Path (Join-Path $templateDirectory $TemplateName) -Raw
    $content = $content.Replace("__TEST_PROJECT_NAME__", $TestProjectName)
    $content = $content.Replace("__TEST_NAMESPACE__", $testNamespace)
    $content = $content.Replace("__TARGET_PROJECT_PATH__", $relativeTargetProjectPath)
    $content = $content.Replace("__TARGET_PROJECT_NAME__", $targetProjectBaseName)
    $content = $content.Replace("__TARGET_FRAMEWORK__", $targetFramework)
    $content = $content.Replace("__AVALONIA_VERSION__", $avaloniaVersion)
    Set-Content -Path (Join-Path $TestProjectDirectory $DestinationName) -Value $content -NoNewline
}

New-FileFromTemplate -TemplateName "HeadlessTests.csproj.template" -DestinationName "$TestProjectName.csproj"
New-FileFromTemplate -TemplateName "TestAppBuilder.cs.template" -DestinationName "TestAppBuilder.cs"
New-FileFromTemplate -TemplateName "TestApp.cs.template" -DestinationName "TestApp.cs"
New-FileFromTemplate -TemplateName "TestOutputHelper.cs.template" -DestinationName "TestOutputHelper.cs"
New-FileFromTemplate -TemplateName "HeadlessGifHarness.cs.template" -DestinationName "HeadlessGifHarness.cs"
New-FileFromTemplate -TemplateName "RenderingSmokeTests.cs.template" -DestinationName "RenderingSmokeTests.cs"
New-FileFromTemplate -TemplateName "gitignore.template" -DestinationName ".gitignore"

if (-not $relativeTestProjectDirectory.StartsWith("../")) {
    $excludePattern = "$relativeTestProjectDirectory/**"
    $currentProjectContent = Get-Content -Path $resolvedTargetProjectPath -Raw
    if (-not $currentProjectContent.Contains($excludePattern)) {
        $exclusionBlock = @"
  <ItemGroup>
    <Compile Remove="$excludePattern" />
    <EmbeddedResource Remove="$excludePattern" />
    <None Remove="$excludePattern" />
    <Content Remove="$excludePattern" />
    <Page Remove="$excludePattern" />
    <AvaloniaResource Remove="$excludePattern" />
  </ItemGroup>
"@
        $updatedProjectContent = $currentProjectContent.Replace("</Project>", "$exclusionBlock`n</Project>")
        Set-Content -Path $resolvedTargetProjectPath -Value $updatedProjectContent -NoNewline
        Write-Host "Updated $resolvedTargetProjectPath to exclude $excludePattern"
    }
}

$generatedProjectPath = Join-Path $TestProjectDirectory "$TestProjectName.csproj"

if (-not [string]::IsNullOrWhiteSpace($SolutionPath) -and (Test-Path $SolutionPath)) {
    dotnet sln $SolutionPath add $generatedProjectPath | Out-Null
    Write-Host "Added $generatedProjectPath to $SolutionPath"
}

Write-Host "Created $generatedProjectPath"
Write-Host "Run: dotnet test `"$generatedProjectPath`""
