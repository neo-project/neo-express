$USER_PATH = "$env:USERPROFILE\.neo-express\default.neo-express"
$NEOXP = "C:\NEO\neo-express\src\neoxp\bin\Release\net10.0\neoxp.exe"
$NEOTRACE = "C:\NEO\neo-express\src\trace\bin\Release\net10.0\neotrace.exe"
$INVOKE_JSON = ".\test-storage.neo-invoke.json"

Write-Host "--- 1. Preparando Red ---" -ForegroundColor Cyan
if (Test-Path $USER_PATH) { Remove-Item $USER_PATH -Force }
& $NEOXP create --force

Write-Host "--- 2. Creando Wallet y Fondos ---" -ForegroundColor Cyan
& $NEOXP wallet create user1
& $NEOXP transfer 1000 GAS genesis user1
& $NEOXP transfer 1000 NEO genesis user1

Write-Host "--- 3. Despliegue de Contrato ---" -ForegroundColor Cyan
# El despliegue genera la infraestructura necesaria para la traza
& $NEOXP contract deploy .\StorageTest.nef user1

Write-Host "--- 4. Invocacion (Correccion Case-Sensitivity) ---" -ForegroundColor Cyan
$INVOKE_JSON = ".\test-storage.neo-invoke.json"
$jsonContent = @"
[
  {
    "contract": "StorageTest",
    "operation": "put",
    "args": [
      { "type": "String", "value": "clave_final" },
      { "type": "String", "value": "funciona" }
    ]
  }
]
"@
$jsonContent | Out-File -FilePath $INVOKE_JSON -Encoding utf8

# Ejecutamos la invocacion
& $NEOXP contract invoke $INVOKE_JSON user1 --trace

Write-Host "--- 5. Verificacion con NEOTRACE ---" -ForegroundColor Cyan
# Buscamos el archivo .neo-trace generado por tu código C#
Start-Sleep -Seconds 2
$TRACE_FILE = Get-ChildItem *.neo-trace | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($TRACE_FILE) {
    Write-Host "¡EXITO! Archivo de traza detectado: $($TRACE_FILE.Name)" -ForegroundColor Green
    & $NEOTRACE $TRACE_FILE.FullName
} else {
    Write-Host "ERROR: No se encontro el archivo .neo-trace. Revisa si el motor genero la traza." -ForegroundColor Red
}

Write-Host "--- 6. Prueba de Run ---" -ForegroundColor Cyan
Write-Host "Iniciando nodo para persistir cambios..." -ForegroundColor Yellow
& $NEOXP run --seconds-per-block 1