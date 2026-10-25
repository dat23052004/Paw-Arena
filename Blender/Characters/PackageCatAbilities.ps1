$ErrorActionPreference = 'Stop'
$exportRoot = Join-Path $PSScriptRoot 'Exports/CatAbilities'
$previewRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Previews/Characters/Abilities'))
$validationRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Validation/UnityRigCheck'))
$validationLog = Get-Content -LiteralPath (Join-Path $validationRoot 'AbilityValidation_Verified.log') -Raw
if (-not $validationLog.Contains('CAT_ANIMATION_VALIDATION_PASS')) { throw 'Final Unity validation is not complete.' }
$audit = Get-Content -LiteralPath (Join-Path $exportRoot 'Ability_Final_Audit.json') -Raw | ConvertFrom-Json
$unity = Get-Content -LiteralPath (Join-Path $exportRoot 'Unity_Animation_Validation.json') -Raw | ConvertFrom-Json
if (-not $audit.passed -or -not $unity.passed -or $unity.clips.Count -ne 16) { throw 'Ability validation failed.' }
Copy-Item -LiteralPath (Join-Path $validationRoot 'Assets/_Project/Animations/Characters/Cat/Abilities/Cat_AllBones.mask') -Destination $exportRoot -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = Join-Path $PSScriptRoot 'PawArena_Cat_Abilities_Package.zip'
$zipStream = [IO.File]::Open($zipPath, [IO.FileMode]::Create, [IO.FileAccess]::Write)
$archive = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Create)
try {
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,
        (Join-Path $PSScriptRoot 'PawArena_Cat_Abilities.blend'), 'PawArena_Cat_Abilities.blend',
        [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $exportRoot -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($exportRoot, $file.FullName).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, "Exports/$relative",
            [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    $previews = @(Get-ChildItem -LiteralPath $previewRoot -Filter '*.mp4' -File | Where-Object { -not $_.Name.EndsWith('_cycle.mp4') })
    if ($previews.Count -ne 17) { throw "Expected 16 previews plus compilation, found $($previews.Count)." }
    $previews += Get-Item -LiteralPath (Join-Path $previewRoot '00_Ability_ContactSheet.png')
    foreach ($file in $previews) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, "Preview/$($file.Name)",
            [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
    $zipStream.Dispose()
}
$check = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $fbx = @($check.Entries | Where-Object { $_.FullName -match '^Exports/AN_Cat_Ability_.+\.fbx$' })
    if ($fbx.Count -ne 16) { throw 'Incorrect FBX clip count in package.' }
    foreach ($entry in $check.Entries) {
        $stream = $entry.Open()
        try {
            $hasher = [Security.Cryptography.SHA256]::Create()
            $null = $hasher.ComputeHash($stream)
            $hasher.Dispose()
        }
        finally { $stream.Dispose() }
    }
    Write-Output "PACKAGE_VERIFIED entries=$($check.Entries.Count) ability_fbx=$($fbx.Count)"
}
finally { $check.Dispose() }
Get-Item -LiteralPath $zipPath | Select-Object FullName,Length
