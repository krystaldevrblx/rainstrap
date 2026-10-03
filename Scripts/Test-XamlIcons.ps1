# Checks every symbol literal used in the settings XAML against the Wpf.Ui icon enums.
#
# An invalid icon name is not a build error. WPF-UI resolves Icon="..." through a
# TypeConverterMarkupExtension at XAML *load* time, so a typo like Icon="Stack2"
# compiles cleanly and then throws XamlParseException the first time the settings
# window is opened - taking the whole window down, not just one button.
#
# Usage:
#   powershell -File .\Scripts\Test-XamlIcons.ps1
param(
    [string]$UiRoot = "Bloxstrap\UI"
)

$ErrorActionPreference = 'Stop'

function Get-EnumValues([string]$enumPath) {
    if (-not (Test-Path $enumPath)) { throw "Icon enum not found: $enumPath" }

    # No comma wrapper: this must unroll so the caller sees individual names.
    Select-String -Path $enumPath -Pattern '^\s{4}(\w+) = 0x' |
        ForEach-Object { $_.Matches[0].Groups[1].Value }
}

$valid = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::Ordinal)

foreach ($enum in @('SymbolRegular', 'SymbolFilled')) {
    foreach ($value in Get-EnumValues "wpfui\src\Wpf.Ui\Common\$enum.cs") {
        [void]$valid.Add([string]$value)
    }
}

Write-Host "known symbols: $($valid.Count) (SymbolRegular + SymbolFilled)"

if ($valid.Count -lt 1000) { throw "Only $($valid.Count) symbols loaded; the enum parse is broken" }

# Attributes whose value is parsed as a symbol: ui:SymbolIcon's Symbol, and the
# Icon property on the Wpf.Ui controls that use IIconControl.
$pattern = '(?m)\b(?:Symbol|Icon)\s*=\s*"([^"{][^"]*)"'

$problems = 0
$checked = 0

foreach ($file in Get-ChildItem $UiRoot -Recurse -Filter *.xaml) {
    $relative = $file.FullName.Substring((Resolve-Path '.').Path.Length + 1)

    $text = Get-Content $file.FullName -Raw
    $lineNumber = 0

    foreach ($match in [regex]::Matches($text, $pattern)) {
        $lineNumber += ([regex]::Matches($text.Substring(0, $match.Index), "`n")).Count + 1
        $symbol = $match.Groups[1].Value.Trim()

        # Skip anything that is not a symbol literal: resource keys, style
        # references, or a pack:// image URI (ui:TitleBar.Icon is an image, not a
        # SymbolRegular, so a colon means this attribute is not an icon name).
        if ($symbol -match '\{|:' -or $symbol -notmatch '^[A-Za-z]\w*$') { continue }

        $checked++

        if ($valid.Contains($symbol)) { continue }

        Write-Host "  INVALID  $relative  '$symbol'"
        $problems++
    }
}

Write-Host ''
Write-Host "checked $checked icon literal(s)"

if ($problems -gt 0) {
    Write-Host "FAILED: $problems invalid symbol name(s)"
    exit 1
}

Write-Host 'all icon names resolve to a known Wpf.Ui symbol'