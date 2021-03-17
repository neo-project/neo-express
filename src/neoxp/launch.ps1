param([int]$secondsPerBlock = 5, [switch]$debug, [string]$checkpoint, [switch]$trace)

dotnet publish -o .\bin\launch

$expressDllPath = '.\bin\launch\neoxp.dll'
$traceArg = if ($trace) { "--trace" } else { "" }

function launch($index) {
	cmd /c start dotnet $expressDllPath run $index --seconds-per-block $secondsPerBlock $traceArg 
}

if ([string]::IsNullOrEmpty($checkpoint)) {
	$privatenet = Get-Content .\default.neo-express | convertfrom-json
	$lastNodeIndex = $privatenet.'consensus-nodes'.Count - 1
	$nodes = if ($debug) { 1..$lastNodeIndex } else { 0..$lastNodeIndex }
	$nodes | ForEach-Object { launch $_ }
} else {
	cmd /c start dotnet $expressDllPath checkpoint run $checkpoint --seconds-per-block $secondsPerBlock $traceArg
}