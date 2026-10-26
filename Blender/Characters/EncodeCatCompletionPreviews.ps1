param([switch]$StillsOnly)
$ErrorActionPreference = 'Stop'
$previewRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Previews/Characters/Completion'))
$clips = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Exports/CatCompletion/Clip_Manifest.json') -Raw | ConvertFrom-Json).clips
$ffmpegPath = 'E:/Tools/ffmpeg/bin/ffmpeg.exe'
$font = 'C\:/Windows/Fonts/segoeuib.ttf'
$movieFiles = @()
if (-not $StillsOnly) {
    for ($index = 0; $index -lt $clips.Count; $index++) {
        $clip = $clips[$index]
        $name = $clip.name
        $label = $name.Replace('_', ' ').ToUpperInvariant()
        $number = ($index + 1).ToString('00')
        $type = if ($clip.loop) { 'LOOP' } else { 'ONE SHOT' }
        $seconds = ($clip.frames / 30.0).ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)
        $raw = Join-Path $previewRoot "${number}_${name}_cycle.mp4"
        $out = Join-Path $previewRoot "${number}_${name}.mp4"
        $filter = "pad=960:540:0:30:color=0x0c1621,drawtext=fontfile='$font':text='PAW ARENA / COMPLETION':x=505:y=80:fontsize=24:fontcolor=0x89b6cb,drawtext=fontfile='$font':text='$label':x=505:y=150:fontsize=28:fontcolor=white,drawtext=fontfile='$font':text='$number / 18    $type':x=505:y=210:fontsize=22:fontcolor=0xfbb75d,drawtext=fontfile='$font':text='$seconds seconds / source 30 fps':x=505:y=255:fontsize=20:fontcolor=0xb1c0cf,drawtext=fontfile='$font':text='15 fps render / repeated for review':x=505:y=360:fontsize=19:fontcolor=0xb1c0cf,drawtext=fontfile='$font':text='In-place body animation':x=505:y=395:fontsize=20:fontcolor=0xb1c0cf"
        if (-not $clip.loop) { $filter += ',tpad=stop_mode=clone:stop_duration=0.4' }
        & $ffmpegPath -hide_banner -loglevel error -y -framerate 15 -start_number 1 -i (Join-Path $previewRoot "Frames/$name/%04d.png") -vf $filter -c:v libx264 -crf 19 -pix_fmt yuv420p -movflags +faststart $raw
        if ($LASTEXITCODE -ne 0) { throw "Encoding failed: $name" }
        & $ffmpegPath -hide_banner -loglevel error -y -stream_loop -1 -i $raw -t 4.8 -c:v libx264 -crf 19 -pix_fmt yuv420p -movflags +faststart $out
        if ($LASTEXITCODE -ne 0) { throw "Review failed: $name" }
        $movieFiles += $out
        Write-Output "ENCODED $name"
    }
    $arguments = @('-hide_banner', '-loglevel', 'error', '-y')
    foreach ($movie in $movieFiles) { $arguments += @('-i', $movie) }
    $streams = (0..17 | ForEach-Object { "[$($_):v]" }) -join ''
    $arguments += @('-filter_complex', "${streams}concat=n=18:v=1:a=0[v]", '-map', '[v]', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', (Join-Path $previewRoot '00_All_18_Completion.mp4'))
    & $ffmpegPath @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Combined preview failed.' }
}
$arguments = @('-hide_banner', '-loglevel', 'error', '-y')
$parts = @()
for ($index = 0; $index -lt $clips.Count; $index++) {
    $number = ($index + 1).ToString('00')
    $label = $clips[$index].name.Replace('_', ' ')
    $arguments += @('-i', (Join-Path $previewRoot "$($number)_$($clips[$index].name).png"))
    $parts += "[$($index):v]scale=320:320,pad=320:355:0:0:color=0x0c1621,drawtext=fontfile='$font':text='$number  $label':x=10:y=329:fontsize=16:fontcolor=white[s$index]"
}
for ($row = 0; $row -lt 3; $row++) {
    $inputs = (($row * 6)..($row * 6 + 5) | ForEach-Object { "[s$_]" }) -join ''
    $parts += "${inputs}hstack=inputs=6[row$row]"
}
$parts += '[row0][row1][row2]vstack=inputs=3[sheet]'
$arguments += @('-filter_complex', ($parts -join ';'), '-map', '[sheet]', '-frames:v', '1', '-update', '1', (Join-Path $previewRoot '00_Completion_ContactSheet.png'))
& $ffmpegPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Contact sheet failed.' }
Write-Output 'COMPLETION_PREVIEWS_COMPLETE'
