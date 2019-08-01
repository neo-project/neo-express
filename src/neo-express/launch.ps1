param([int]$secondsPerBlock = 1, [switch]$debug, [switch]$reset, [string]$checkpoint)

dotnet publish

$expressDllPath = '.\bin\Debug\netcoreapp2.1\publish\neo-express.dll'

function launch($index) {
	$resetArg = if ($reset) { "--reset" } else { "" }
	cmd /c start dotnet $expressDllPath run $index --seconds-per-block $secondsPerBlock $resetArg 
}

if ([string]::IsNullOrEmpty($checkpoint)) {
	$privatenet = Get-Content .\express.privatenet.json | convertfrom-json
	$lastNodeIndex = $privatenet.'consensus-nodes'.Count - 1
	$nodes = if ($debug) { 1..$lastNodeIndex } else { 0..$lastNodeIndex }
	$nodes | ForEach-Object { launch $_ }
} else {
	cmd /c start dotnet $expressDllPath checkpoint run $checkpoint --seconds-per-block $secondsPerBlock
}