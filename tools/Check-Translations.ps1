<#
.SYNOPSIS
    Checks the UI string resources (GlassMonitor/Resources/Strings*.resx).

.DESCRIPTION
    - Every key used in XAML ({l:Loc Key}) and C# ("Area_Name" string literals) exists in Strings.resx.
    - Every translation (Strings.<code>.resx) has the same keys as Strings.resx and the same {0} placeholders.
    - Keys in Strings.resx that are not used anywhere are reported as warnings.
    Missing translations only produce warnings, because the app falls back to English for them.
    Exits with code 1 if there are errors.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/Check-Translations.ps1
#>
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\GlassMonitor'
$resources = Join-Path $project 'Resources'

function Read-Resx([string]$path) {
    $strings = [ordered]@{}
    foreach ($data in ([xml](Get-Content -LiteralPath $path -Raw -Encoding UTF8)).root.data) {
        $strings[$data.name] = [string]$data.value
    }
    return $strings
}

function Get-Placeholders([string]$text) {
    return (([regex]::Matches($text, '\{\d+\}') | ForEach-Object Value | Sort-Object -Unique) -join ' ')
}

$errors = 0
$warnings = 0
function Report([string]$level, [string]$message) {
    if ($level -eq 'error') { $script:errors++ } else { $script:warnings++ }
    Write-Host "${level}: $message" -ForegroundColor $(if ($level -eq 'error') { 'Red' } else { 'Yellow' })
}

$neutral = Read-Resx (Join-Path $resources 'Strings.resx')

# Keys used in the source code
$used = [System.Collections.Generic.HashSet[string]]::new()
foreach ($file in Get-ChildItem $project -Recurse -Include *.xaml, *.cs | Where-Object FullName -NotMatch '\\(bin|obj)\\') {
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $pattern = if ($file.Extension -eq '.xaml') { '\{l:Loc\s+(?:Key=)?(\w+)\}' } else { '"([A-Z][A-Za-z0-9]*_[A-Za-z0-9_]+)"' }
    foreach ($match in [regex]::Matches($text, $pattern)) {
        $key = $match.Groups[1].Value
        [void]$used.Add($key)
        if (-not $neutral.Contains($key)) {
            Report 'error' "$($file.Name): '$key' is not in Strings.resx"
        }
    }
}
foreach ($key in $neutral.Keys) {
    if (-not $used.Contains($key)) { Report 'warning' "Strings.resx: '$key' is not used" }
}

# Translations
foreach ($file in Get-ChildItem $resources -Filter 'Strings.*.resx') {
    $translation = Read-Resx $file.FullName
    foreach ($key in $neutral.Keys) {
        if (-not $translation.Contains($key)) {
            Report 'warning' "$($file.Name): '$key' is not translated (English is shown)"
        }
        elseif ((Get-Placeholders $translation[$key]) -ne (Get-Placeholders $neutral[$key])) {
            Report 'error' "$($file.Name): '$key' must have the same placeholders as English ($(Get-Placeholders $neutral[$key]))"
        }
    }
    foreach ($key in $translation.Keys) {
        if (-not $neutral.Contains($key)) { Report 'error' "$($file.Name): '$key' is not in Strings.resx" }
    }
}

Write-Host "$($neutral.Count) strings, $errors error(s), $warnings warning(s)"
if ($errors -gt 0) { exit 1 }
