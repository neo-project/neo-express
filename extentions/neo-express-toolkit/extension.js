// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
const vscode = require('vscode');
const path = require('path');

// This method is called when your extension is activated
// Your extension is activated the very first time the command is executed

/**
 * @param {vscode.ExtensionContext} context
 */
function activate(context) {

	// Use the console to output diagnostic information (console.log) and errors (console.error)
	// This line of code will only be executed once when your extension is activated
	console.log('Congratulations, your extension "neo-express-toolkit" is now active!');

	// The command has been defined in the package.json file
	// Now provide the implementation of the command with  registerCommand
	// The commandId parameter must match the command field in package.json
	let disposable = vscode.commands.registerCommand('neo-express-toolkit.helloWorld', function () {
		// The code you place here will be executed every time your command is executed

		// Display a message box to the user
		vscode.window.showInformationMessage('Hello World from Neo Express Toolkit!');

		const panel = prepareWebView(context);

		panel.webview.onDidReceiveMessage(
			async ({ message }) => {
				vscode.window.showInformationMessage(message);
			},
			undefined,
			context.subscriptions
		);
	});

	context.subscriptions.push(disposable);
}

function prepareWebView(context)
{
	const panel = vscode.window.createWebviewPanel(
		"vueWebview",
		"vue webview",
		vscode.ViewColumn.One,
		{
			enableScripts: true,
			localResourceRoots: [
				vscode.Uri.file(
					path.join(context.extensionPath, "panel", "dist", "assets")
				),
			],
		}
	);

	const dependencyNameList = [
        "index.css",
        "index.js",
        "vendor.js",
        "logo.png",
    ];

	const dependencyList = dependencyNameList.map((item) =>
		panel.webview.asWebviewUri(
			vscode.Uri.file(
				path.join(context.extensionPath, "panel", "dist", "assets", item)
			)
		)
	);

	const html = `<!DOCTYPE html>
	<html lang="en">
	<head>
		<meta charset="UTF-8" />
		<link rel="icon" href="/favicon.ico" />
		<meta name="viewport" content="width=device-width, initial-scale=1.0" />
		<title>Vite App</title>
		<script>
			const vscode = acquireVsCodeApi();
		</script>
		<script type="module" crossorigin src="${dependencyList[1]}"></script>
		<link rel="modulepreload" href="${dependencyList[2]}">
		<link rel="stylesheet" href="${dependencyList[0]}">
	</head>
	<body>
		<div id="app"></div>
	</body>
	</html>
	`;

	panel.webview.html = html;
	return panel
}

// This method is called when your extension is deactivated
function deactivate() {}

module.exports = {
	activate,
	deactivate,
	prepareWebView,
}
