# Script: Crear ZIP para IA sin borrar nada del proyecto

$root = Get-Location

$zipPath = Join-Path (Split-Path $root -Parent) 'renderset.zip'

$excludedFolders = @(
    '\bin\',
    '\obj\',
    '\.git\',
    '\.vs\',
    '\node_modules\'
)

$excludedFiles = @(
    '.user',
    '.suo',
    '.cache'
)

# Borra solo el ZIP anterior, no toca el proyecto
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Creamos carpeta temporal fuera del proyecto
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("renderset_zip_" + [Guid]::NewGuid().ToString("N"))

New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    Get-ChildItem -Path $root -Recurse -File -Force |
        Where-Object {
            $fullName = $_.FullName

            $isExcludedFolder = $false

            foreach ($folder in $excludedFolders) {
                if ($fullName.Contains($folder)) {
                    $isExcludedFolder = $true
                    break
                }
            }

            if ($isExcludedFolder) {
                return $false
            }

            foreach ($filePattern in $excludedFiles) {
                if ($_.Name.EndsWith($filePattern)) {
                    return $false
                }
            }

            return $true
        } |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($root.Path.Length).TrimStart('\')
            $destination = Join-Path $tempRoot $relativePath
            $destinationFolder = Split-Path $destination -Parent

            if (!(Test-Path $destinationFolder)) {
                New-Item -ItemType Directory -Path $destinationFolder -Force | Out-Null
            }

            Copy-Item $_.FullName $destination -Force
        }

    Compress-Archive `
        -Path (Join-Path $tempRoot '*') `
        -DestinationPath $zipPath `
        -Force

    Write-Host "ZIP creado en: $zipPath"
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item $tempRoot -Recurse -Force
    }
}