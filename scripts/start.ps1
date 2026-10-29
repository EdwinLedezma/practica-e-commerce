param([int]$GatewayPort=5100,[int]$CatalogPort=5101,[int]$InventoryPort=5102)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$processes=@()
try {
    dotnet build (Join-Path $projectRoot 'ECommerce.slnx') --nologo
    if($LASTEXITCODE -ne 0) { throw 'La compilación falló.' }
    $env:Services__CatalogUrl="http://localhost:$CatalogPort/"
    $env:Services__InventoryUrl="http://localhost:$InventoryPort/"
    foreach($entry in @(@('Catalog',$CatalogPort),@('Inventory',$InventoryPort),@('Gateway',$GatewayPort))) {
        $dll=Join-Path $projectRoot "src/$($entry[0]).Api/bin/Debug/net10.0/$($entry[0]).Api.dll"
        $contentRoot=Join-Path $projectRoot "src/$($entry[0]).Api"
        $processes+=Start-Process dotnet -ArgumentList @("`"$dll`"",'--urls',"http://localhost:$($entry[1])",'--contentRoot',"`"$contentRoot`"") -PassThru -WindowStyle Hidden
    }
    Write-Host "Dashboard: http://localhost:$GatewayPort · Ctrl+C para detener los tres procesos."
    while($true) {
        Start-Sleep -Seconds 1
        foreach($process in $processes) { if($process.HasExited) { throw "Un servicio terminó con código $($process.ExitCode). Revisa puertos y configuración." } }
    }
} finally {
    foreach($process in $processes) { if(!$process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue } }
}
