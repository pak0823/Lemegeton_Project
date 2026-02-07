$path = "Assets/01_Scripts/Exploration/PlayerMovement.cs"
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)

$lines[28] = '    [Header("타일맵 설정")]'
$lines[70] = '    [Header("자동경로 이동 설정")]'
$lines[158] = '    [Header("점프 이동 설정")]'

for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match 'Debug\.Log\(\$"\[PlayerMovement\]') {
        $lines[$i] = '        Debug.Log($"[PlayerMovement] 맵 설정 완료. 바닥 맵 개수: {_floors?.Count ?? 0}");'
    }
    # Match // "?동" pattern (garbled)
    if ($lines[$i] -match '// ".*동"') {
        $lines[$i] = $lines[$i] -replace '// ".*"', '// "이동"'
    }
}

[System.IO.File]::WriteAllLines($path, $lines, [System.Text.Encoding]::UTF8)
Write-Host "Fixed lines in $path"
