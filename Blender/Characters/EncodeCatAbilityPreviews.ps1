param([switch]$StillsOnly)
$ErrorActionPreference = 'Stop'
$previewRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Previews/Characters/Abilities'))
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Exports/CatAbilities/Clip_Manifest.json') -Raw | ConvertFrom-Json
$clips = $manifest.clips
$ffmpegPath = 'E:/Tools/ffmpeg/bin/ffmpeg.exe'
$font = 'C\:/Windows/Fonts/segoeuib.ttf'
$movieFiles = @()

if (-not $StillsOnly) {
    for ($index = 0; $index -lt $clips.Count; $index++) {
        $clip = $clips[$index]
        $name = $clip.name
        $label = $name.Replace('Ability_', '').Replace('_', ' ').ToUpperInvariant()
        $label = $label.Replace('NINELIVES', 'NINE LIVES').Replace('WINDShurikens'.ToUpperInvariant(), 'WIND SHURIKENS')
        $number = ($index + 1).ToString('00')
        $type = if ($clip.loop) { 'LOOP' } else { 'ONE SHOT' }
        $seconds = ($clip.frames / 30.0).ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)
        $inputPattern = Join-Path $previewRoot "Frames/$name/%04d.png"
        $raw = Join-Path $previewRoot "${number}_${name}_cycle.mp4"
        $out = Join-Path $previewRoot "${number}_${name}.mp4"
        $filter = "pad=1280:800:20:40:color=0x0c1621,drawtext=fontfile='$font':text='PAW ARENA / ABILITIES':x=770:y=100:fontsize=25:fontcolor=0x89b6cb,drawtext=fontfile='$font':text='$label':x=770:y=175:fontsize=28:fontcolor=white,drawtext=fontfile='$font':text='$number / 16     $type':x=770:y=245:fontsize=22:fontcolor=0xfbb75d,drawtext=fontfile='$font':text='$seconds seconds  -  30 fps':x=770:y=292:fontsize=22:fontcolor=0xb1c0cf,drawtext=fontfile='$font':text='BODY ANIMATION ONLY':x=770:y=380:fontsize=20:fontcolor=0xb1c0cf,drawtext=fontfile='$font':text='Travel and VFX added in game':x=770:y=420:fontsize=20:fontcolor=0xb1c0cf,drawtext=fontfile='$font':text='REPEATED FOR REVIEW':x=770:y=660:fontsize=19:fontcolor=0x89b6cb"
        if (-not $clip.loop) { $filter += ',tpad=stop_mode=clone:stop_duration=0.5' }
        & $ffmpegPath -hide_banner -loglevel error -y -framerate 30 -start_number 1 -i $inputPattern -vf $filter -c:v libx264 -crf 19 -pix_fmt yuv420p -movflags +faststart $raw
        if ($LASTEXITCODE -ne 0) { throw "Encoding failed: $name" }
        & $ffmpegPath -hide_banner -loglevel error -y -stream_loop -1 -i $raw -t 4.8 -c:v libx264 -crf 19 -pix_fmt yuv420p -movflags +faststart $out
        if ($LASTEXITCODE -ne 0) { throw "Review loop failed: $name" }
        $movieFiles += $out
        Write-Output "ENCODED $name"
    }
    $arguments = @('-hide_banner', '-loglevel', 'error', '-y')
    foreach ($movie in $movieFiles) { $arguments += @('-i', $movie) }
    $streams = (0..15 | ForEach-Object { "[$($_):v]" }) -join ''
    $arguments += @('-filter_complex', "${streams}concat=n=16:v=1:a=0[v]", '-map', '[v]', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', (Join-Path $previewRoot '00_All_16_Abilities.mp4'))
    & $ffmpegPath @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Combined preview failed.' }
}

$sheetArgs = @('-hide_banner', '-loglevel', 'error', '-y')
$parts = @()
for ($index = 0; $index -lt $clips.Count; $index++) {
    $number = ($index + 1).ToString('00')
    $label = $clips[$index].name.Replace('Ability_', '').Replace('_', ' ')
    $sheetArgs += @('-i', (Join-Path $previewRoot "$($number)_$($clips[$index].name).png"))
    $parts += "[$($index):v]scale=320:320,pad=320:355:0:0:color=0x0c1621,drawtext=fontfile='$font':text='$number  $label':x=10:y=329:fontsize=16:fontcolor=white[s$index]"
}
for ($row = 0; $row -lt 4; $row++) {
    $inputs = (($row * 4)..($row * 4 + 3) | ForEach-Object { "[s$_]" }) -join ''
    $parts += "${inputs}hstack=inputs=4[row$row]"
}
$parts += '[row0][row1][row2][row3]vstack=inputs=4[sheet]'
$sheetArgs += @('-filter_complex', ($parts -join ';'), '-map', '[sheet]', '-frames:v', '1', '-update', '1', (Join-Path $previewRoot '00_Ability_ContactSheet.png'))
& $ffmpegPath @sheetArgs
if ($LASTEXITCODE -ne 0) { throw 'Contact sheet failed.' }
Write-Output 'ABILITY_PREVIEWS_COMPLETE'
