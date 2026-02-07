$path = "Assets/01_Scripts/Exploration/PlayerMovement.cs"
$content = Get-Content $path -Raw

# 1. Replace Type and Variable Definitions
$content = $content -replace "private BoxInteract pendingChest", "private IInteractable pendingInteractable"
$content = $content -replace "BoxInteract clickedChest", "IInteractable clickedInteractable"

# 2. Replace Assignments and References
$content = $content -replace "pendingChest", "pendingInteractable"
$content = $content -replace "clickedChest", "clickedInteractable"

# 3. Replace Component Retrieval
$content = $content -replace "GetComponentInParent<BoxInteract>", "GetComponentInParent<IInteractable>"
$content = $content -replace "GetComponent<BoxInteract>", "GetComponent<IInteractable>"

# 4. Replace Logic Specifics
# chest.IsOpened -> !chest.CanInteract
# Note: variable might be 'chest' or 'clickedInteractable'
$content = $content -replace "\.IsOpened", ".CanInteract == false"
# Or better logic: if (chest.IsOpened) -> if (!chest.CanInteract)
# But simplified replacement: .IsOpened is boolean.
# Wait, IInteractable has CanInteract (bool).
# Original: if (chest.IsOpened) continue;
# New: if (chest.CanInteract == false) continue; -> valid C#
# But IsOpened means "Open", CanInteract means "Closed" (effectively).
# So IsOpened == true => CanInteract == false.
# So .IsOpened replacement with .CanInteract == false is correct logic wise?
# Yes. if (true) vs if (false == false).
# Correct.

# 5. Replace Method Calls
$content = $content -replace "\.OpenChest\(\)", ".OnInteract()"

# 6. Fix specific block variable name 'chest' if it was local var
# Logic: var chest = ...; if (chest != null) ...
# I want to rename local var 'chest' to 'interactable' for clarity, but strictly speaking replacing 'chest' with 'interactable' word-boundary is safer.
# However, 'chest' might be used elsewhere? grep showed only in that block.
# Let's replace 'chest' with 'interactable' inside the specific block if possible, or just globally if 'chest' is unique enough?
# 'chest' is a common word.
# Let's use simpler approach: The existing code uses 'chest' local var.
# var chest = h.GetComponentInParent<IInteractable>();
# if (chest != null) ...
# This logic works fine even if var name is 'chest'.
# I just need to make sure 'chest' is indeed IInteractable (which step 3 ensures).

# 7. One edge case: "BoxInteract(상자)" comment
# No functional impact.

Set-Content $path $content
Write-Host "Migration applied to $path"
