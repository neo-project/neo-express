param([int]$secondsPerBlock = 1, [switch]$debug)

dotnet publish
function launch($index) {
	cmd /c start dotnet .\bin\Debug\netcoreapp2.2\publish\neo-express.dll run $index --seconds-per-block $secondsPerBlock
}

$privatenet = Get-Content .\express.privatenet.json | convertfrom-json
$lastNodeIndex = $privatenet.'consensus-nodes'.Count - 1
$nodes = if ($debug) { 1..$lastNodeIndex } else { 0..$lastNodeIndex }
$nodes | ForEach-Object { launch $_ }