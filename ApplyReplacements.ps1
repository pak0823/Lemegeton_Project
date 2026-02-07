$rep = Get-Content "replacements.txt" -Encoding UTF8
$path = "Assets/01_Scripts/Exploration/PlayerMovement.cs"
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)

$lines[28] = $rep[0]
$lines[70] = $rep[1]
$lines[158] = $rep[2]

for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match 'Debug\.Log\(\$"\[PlayerMovement\]') {
        $lines[$i] = $rep[3]
    }
    if ($lines[$i] -match 'InteractionHintUI.*ShowSurveyAt') {
        # Replace comment at the end
        $lines[$i] = $lines[$i] -replace '//.*', $rep[4]
    }
}

[System.IO.File]::WriteAllLines($path, $lines, [System.Text.Encoding]::UTF8)
Write-Host "Applied replacements from file."
