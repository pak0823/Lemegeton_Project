$path = "Assets/01_Scripts/Exploration/PlayerMovement.cs"
$text = [System.IO.File]::ReadAllText($path)
[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
Write-Host "Fixed encoding for $path"
