# Define the URL and the target file name for the BepInEx ZIP file
$bepinexUrl = "https://builds.bepinex.dev/projects/bepinex_be/729/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.729%2B35f6b1b.zip"
$zipFile = "bepinex.zip"

# Function to find the Yu-Gi-Oh! Master Duel installation path
function Get-GamePath {
    $defaultPaths = @(
        "$pwd",
        "C:\Program Files (x86)\Steam\steamapps\common\Yu-Gi-Oh!  Master Duel",
        "A:\Steam\steamapps\common\Yu-Gi-Oh!  Master Duel",
        "D:\Steam\steamapps\common\Yu-Gi-Oh!  Master Duel"
    )

    foreach ($path in $defaultPaths) {
        if ((Test-Path $path) -and ($path -like "*Steam\steamapps\common\Yu-Gi-Oh!  Master Duel")) {
            return $path
        }
    }

    # If no default path is found, ask the user for the installation path
    Write-Host "Could not find the Yu-Gi-Oh! Master Duel installation folder."
    $customPath = Read-Host "Please enter the full path to your Yu-Gi-Oh! Master Duel folder"
    if (Test-Path $customPath) {
        return $customPath
    } else {
        Write-Host "Invalid path provided. Exiting script."
        exit
    }
}

# Get the installation path
$installPath = Get-GamePath

# Define the destination folder for extraction
$destinationFolder = $installPath

# Download the BepInEx ZIP file
Invoke-WebRequest -Uri $bepinexUrl -OutFile $zipFile

# Check if the download was successful
if (Test-Path $zipFile) {
    Write-Host "Download completed, extracting the file to $destinationFolder..."
    
    # Extract the BepInEx ZIP file
    Expand-Archive -Path $zipFile -DestinationPath $destinationFolder -Force

    Write-Host "Extraction completed."
    
    $response = Invoke-RestMethod -Uri "https://api.github.com/repos/radsi/Master-Duels-BlindMode/releases/latest"
    $downloadUrl = ($response.assets | Where-Object { $_.name -like "*.dll" }).browser_download_url

    # Ensure the plugins folder exists
    $pluginsFolder = Join-Path -Path $destinationFolder -ChildPath 'BepInEx\plugins'
    if (-not (Test-Path $pluginsFolder)) {
        New-Item -ItemType Directory -Path $pluginsFolder | Out-Null
    }

    # Download the latest release .dll file from GitHub
    Invoke-WebRequest -Uri $downloadUrl -OutFile (Join-Path -Path $pluginsFolder -ChildPath 'BlindMode.dll')

    Write-Host "DLL downloaded and placed in BepInEx\plugins."

    # Delete the downloaded ZIP file
    Remove-Item -Path $zipFile -Force
} else {
    Write-Host "There was an error downloading the BepInEx file."
}