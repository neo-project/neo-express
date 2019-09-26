param([int]$secondsPerBlock = 1, [switch]$debug, [switch]$reset, [string]$checkpoint)

dotnet publish -o .\bin\launch

$expressDllPath = '.\bin\launch\neo-express.dll'

function launch($index) {
	$resetArg = if ($reset) { "--reset" } else { "" }
	cmd /c start dotnet $expressDllPath run $index --seconds-per-block $secondsPerBlock $resetArg 
}

if ([string]::IsNullOrEmpty($checkpoint)) {
	$privatenet = Get-Content .\default.neo-express.json | convertfrom-json
	$lastNodeIndex = $privatenet.'consensus-nodes'.Count - 1
	$nodes = if ($debug) { 1..$lastNodeIndex } else { 0..$lastNodeIndex }
	$nodes | ForEach-Object { launch $_ }
} else {
	cmd /c start dotnet $expressDllPath checkpoint run $checkpoint --seconds-per-block $secondsPerBlock
}