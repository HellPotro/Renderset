# Carpetas que no queremos incluir
$foldersToRemove = @(
    'bin',
    'obj',
    '.git',
    '.vs',
    'node_modules'
)

# Busca todas las carpetas a eliminar
# Ordenamos de las más profundas a las menos profundas
# para evitar problemas al borrar durante el recorrido.
Get-ChildItem -Path . -Recurse -Directory -Force -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -in $foldersToRemove
    } |
    Sort-Object { $_.FullName.Length } -Descending |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# Ruta del ZIP
$zipPath = Join-Path (Split-Path (Get-Location) -Parent) 'renderset.zip'

# Borra el ZIP anterior si existe
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Comprime el proyecto limpio
Compress-Archive `
    -Path (Join-Path (Get-Location) '*') `
    -DestinationPath $zipPath `
    -Force

Write-Host "ZIP creado en: $zipPath"