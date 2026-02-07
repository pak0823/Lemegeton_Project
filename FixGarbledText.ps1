$path = "Assets/01_Scripts/Exploration/PlayerMovement.cs"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

# 1. Header Fixes
# Matches [Header("??맵 ?정")]
$content = $content -replace '\[Header\(".*맵.*"\)\]', '[Header("타일맵 설정")]'

# Matches [Header("???경로 ?동 ?정")]
$content = $content -replace '\[Header\(".*경로.*"\)\]', '[Header("자동경로 이동 설정")]'

# Matches [Header("?이 ?동 ?정")]
# Use a more specific pattern to avoid false positives
$content = $content -replace '\[Header\("\?이 \?동 \?정"\)\]', '[Header("점프 이동 설정")]'

# 2. Debug.Log Fix
# Matches Debug.Log($"[PlayerMovement] ??정 ?료...
# Pattern: Debug.Log($"[PlayerMovement] ... {_floors...
$content = $content -replace 'Debug\.Log\(\$"\[PlayerMovement\] .*_floors\?\.Count.*\);', 'Debug.Log($"[PlayerMovement] 맵 설정 완료. 바닥 맵 개수: {_floors?.Count ?? 0}");'

# 3. Comment Fixes
# // "?동" -> // "이동"
$content = $content -replace '// "\?동"', '// "이동"'

[System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
Write-Host "Fixed garbled text in $path"
