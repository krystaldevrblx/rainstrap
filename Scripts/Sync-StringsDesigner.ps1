# Regenerates Resources/Strings.Designer.cs from Resources/Strings.resx.
#
# The designer file is checked in and is normally produced by
# PublicResXFileCodeGenerator inside Visual Studio. That generator is not
# available from the command line, so editing it by hand is the only option here -
# and hand-editing is exactly how the two files drift.
#
# The failure mode is silent and nasty: a resx entry with no designer property
# returns null at runtime, producing a blank label in the UI or an exception
# inside String.Format, while a designer property with no resx entry is dead code.
# Neither is a build error. This script rebuilds the property list from the resx so
# the two cannot disagree.
#
# Usage:
#   powershell -File .\Scripts\Sync-StringsDesigner.ps1
param(
    [string]$Resx = "Bloxstrap\Resources\Strings.resx",
    [string]$Designer = "Bloxstrap\Resources\Strings.Designer.cs"
)

$ErrorActionPreference = 'Stop'

# Placeholders from the resx schema documentation, not real resources.
$ignored = @('Name1', 'Color1', 'Bitmap1', 'Icon1')

function ConvertTo-PropertyName([string]$key) {
    # Matches StronglyTypedResourceBuilder: anything that cannot appear in an
    # identifier becomes an underscore, so "VersionControl.Details.Support.X"
    # becomes "VersionControl_Details_Support_X".
    return ($key -replace '[^A-Za-z0-9_]', '_')
}

function ConvertTo-Summary([string]$value) {
    # The generator flattens the value onto a single "similar to" line and escapes
    # the three characters that would terminate the XML doc comment.
    $summary = ($value -replace '\s+', ' ').Trim()
    return ($summary -replace '&', '&amp;' -replace '<', '&lt;' -replace '>', '&gt;')
}

# ---------------------------------------------------------------- resx -> entries

[xml]$xml = Get-Content $Resx -Raw

$entries = [ordered]@{}

foreach ($node in $xml.root.data) {
    $key = [string]$node.name

    if ($ignored -contains $key) { continue }

    $entries[$key] = [string]$node.value
}

Write-Host "resx: $($entries.Count) entries"

# ------------------------------------------------- existing designer boilerplate

$existing = Get-Content $Designer -Raw

# Everything up to the first generated property, and everything after the last one.
$firstProperty = $existing.IndexOf('        /// <summary>' + "`r`n" + '        ///   Looks up a localized string similar to ')
if ($firstProperty -lt 0) { $firstProperty = $existing.IndexOf('        /// <summary>' + "`n" + '        ///   Looks up a localized string similar to ') }
if ($firstProperty -lt 0) { throw 'Could not find the first generated property; is this a generated Strings.Designer.cs?' }

$lastProperty = $existing.LastIndexOf('        /// <summary>')
if ($lastProperty -lt 0) { throw 'Could not find the last generated property' }

# Walk back from the last property to the end of the last property block.
$tailStart = $existing.LastIndexOf("`r`n    }`r`n}")
if ($tailStart -lt 0) { $tailStart = $existing.LastIndexOf("`n    }`n}") }
if ($tailStart -lt 0) { throw 'Could not find the end of the Strings class' }

$header = $existing.Substring(0, $firstProperty)
$tail = $existing.Substring($tailStart)

Write-Host "designer: keeping $($header.Length) header bytes and $($tail.Length) tail bytes"

# ------------------------------------------------------------- regenerate body

$body = New-Object System.Collections.Generic.List[string]

foreach ($key in ($entries.Keys | Sort-Object)) {
    $property = ConvertTo-PropertyName $key
    $summary = ConvertTo-Summary $entries[$key]

    $body.Add('        /// <summary>')
    $body.Add("        ///   Looks up a localized string similar to $summary.")
    $body.Add('        /// </summary>')
    $body.Add("        public static string $property {")
    $body.Add('            get {')
    $body.Add("                return ResourceManager.GetString(`"$key`", resourceCulture);")
    $body.Add('            }')
    $body.Add('        }        ')
}

$output = $header + ($body -join "`r`n") + $tail

Set-Content -Path $Designer -Value $output -NoNewline

# ------------------------------------------------------------------ verification

$finalKeys = [regex]::Matches((Get-Content $Designer -Raw), 'ResourceManager\.GetString\("([^"]+)"') |
    ForEach-Object { $_.Groups[1].Value }

$missing = @($finalKeys | Where-Object { -not $entries.Contains($_) })
$orphaned = @($entries.Keys) | Where-Object { $finalKeys -notcontains $_ }

Write-Host ''
Write-Host "generated: $($finalKeys.Count) properties"

if ($missing.Count) { throw "designer has properties with no resx entry: $($missing -join ', ')" }
if ($orphaned.Count) { throw "resx has entries with no designer property: $($orphaned -join ', ')" }

$braces = ([regex]::Matches($output, '\{')).Count - ([regex]::Matches($output, '\}')).Count
if ($braces -ne 0) { throw "unbalanced braces in the generated file (delta $braces)" }

Write-Host 'verified: designer and resx are in step, braces balanced'