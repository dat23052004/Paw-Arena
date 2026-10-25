$ErrorActionPreference = 'Stop'
$previewRoot = Join-Path $PSScriptRoot '../Previews/Characters/Animations01'
$previewRoot = [IO.Path]::GetFullPath($previewRoot)
$ffmpegPath = 'E:/Tools/ffmpeg/bin/ffmpeg.exe'
$font = 'C\:/Windows/Fonts/segoeuib.ttf'
$names = @('Idle', 'Walk', 'Run', 'Jump_Start', 'Jump_Air', 'Fall', 'Land', 'Punch_Left', 'Punch_Right', 'Hit_React')
$durations = @(72, 30, 20, 12, 24, 24, 14, 18, 18, 16)
$loops = @($true, $true, $true, $false, $true, $true, $false, $false, $false, $false)
$movieFiles = @()

for ($index = 0; $index -lt $names.Count; $index++) {
    $name = $names[$index]
    $label = $name.Replace('_', ' ').ToUpperInvariant()
    $number = ($index + 1).ToString('00')
    $type = if ($loops[$index]) { 'LOOP' } else { 'ONE SHOT' }
    $seconds = ($durations[$index] / 30.0).ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)
    $inputPattern = Join-Path $previewRoot "Frames/$name/%04d.png"
    $raw = Join-Path $previewRoot "${number}_${name}_cycle.mp4"
    $out = Join-Path $previewRoot "${number}_${name}.mp4"
    $filter = "pad=1280:800:20:40:color=0x0c1621,drawtext=fontfile='$font':text='PAW ARENA':x=790:y=110:fontsize=26:fontcolor=0x89b6cb,drawtext=fontfile='$font':text='$label':x=790:y=175:fontsize=38:fontcolor=white,drawtext=fontfile='$font':text='$number / 10     $type':x=790:y=245:fontsize=22:fontcolor=0xfbb75d,drawtext=fontfile='$font':text='$seconds seconds  -  30 fps':x=790:y=292:fontsize=22:fontcolor=0xb1c0cf,drawtext=fontfile='$font':text='IN-PLACE ANIMATION':x=790:y=380:fontsize=20:fontcolor=0xb1c0cf,drawtext=fontfile='$font':text='Base and rig unchanged':x=790:y=420:fontsize=20:fontcolor=0xb1c0cf,drawtext=fontfile='$font':text='REPEATED FOR REVIEW':x=790:y=660:fontsize=19:fontcolor=0x89b6cb"
    if (-not $loops[$index]) {
        $filter += ',tpad=stop_mode=clone:stop_duration=0.55'
    }
    & $ffmpegPath -hide_banner -loglevel error -y -framerate 30 -start_number 1 -i $inputPattern -frames:v $durations[$index] -vf $filter -c:v libx264 -crf 19 -pix_fmt yuv420p -movflags +faststart $raw
    if ($LASTEXITCODE -ne 0) { throw "Encoding failed: $name" }
    # Re-encode without a frame limit for one-shots, retaining the settling hold.
    if (-not $loops[$index]) {
        & $ffmpegPath -hide_banner -loglevel error -y -framerate 30 -start_number 1 -i $inputPattern -vf $filter -c:v libx264 -crf 19 -pix_fmt yuv420p -movflags +faststart $raw
        if ($LASTEXITCODE -ne 0) { throw "Hold encoding failed: $name" }
    }
    & $ffmpegPath -hide_banner -loglevel error -y -stream_loop -1 -i $raw -t 4.8 -c:v libx264 -crf 19 -pix_fmt yuv420p -movflags +faststart $out
    if ($LASTEXITCODE -ne 0) { throw "Review loop failed: $name" }
    $movieFiles += $out
    Write-Output "ENCODED $name"
}

$arguments = @('-hide_banner', '-loglevel', 'error', '-y')
foreach ($movie in $movieFiles) { $arguments += @('-i', $movie) }
$streams = (0..9 | ForEach-Object { "[$($_):v]" }) -join ''
$arguments += @('-filter_complex', "${streams}concat=n=10:v=1:a=0[v]", '-map', '[v]', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', (Join-Path $previewRoot '00_All_10_Animations.mp4'))
& $ffmpegPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Combined preview failed.' }

$sheetArgs = @('-hide_banner', '-loglevel', 'error', '-y')
$parts = @()
for ($index = 0; $index -lt $names.Count; $index++) {
    $number = ($index + 1).ToString('00')
    $label = $names[$index].Replace('_', ' ')
    $sheetArgs += @('-i', (Join-Path $previewRoot "$($number)_$($names[$index]).png"))
    $parts += "[$($index):v]scale=360:360,pad=360:402:0:0:color=0x0c1621,drawtext=fontfile='$font':text='$number  $label':x=18:y=371:fontsize=19:fontcolor=white[s$index]"
}
$parts += '[s0][s1][s2][s3][s4]hstack=inputs=5[top]'
$parts += '[s5][s6][s7][s8][s9]hstack=inputs=5[bottom]'
$parts += '[top][bottom]vstack=inputs=2[sheet]'
$sheetArgs += @('-filter_complex', ($parts -join ';'), '-map', '[sheet]', '-frames:v', '1', '-update', '1', (Join-Path $previewRoot '00_Animation_ContactSheet.png'))
& $ffmpegPath @sheetArgs
if ($LASTEXITCODE -ne 0) { throw 'Contact sheet failed.' }
Write-Output 'PREVIEW_ENCODING_COMPLETE'
