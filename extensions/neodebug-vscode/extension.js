// Copyright (C) 2015-2026 The Neo Project.
//
// extension.js file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

// @ts-check
'use strict';

const vscode = require('vscode');

/**
 * Tells VS Code how to launch the debug adapter for the "neo-contract" debug type: it runs the
 * `neodebug` tool (the Neo.Debug global tool) as a stdio Debug Adapter Protocol host. The executable
 * can be overridden with the `neo-contract.debugAdapterPath` setting.
 */
class NeoDebugAdapterDescriptorFactory {
    createDebugAdapterDescriptor(_session, _executable) {
        const configured = vscode.workspace
            .getConfiguration('neo-contract')
            .get('debugAdapterPath');
        const command = (typeof configured === 'string' && configured.length > 0)
            ? configured
            : 'neodebug';
        return new vscode.DebugAdapterExecutable(command, []);
    }
}

/** Fills in sensible defaults for a bare or generated launch configuration. */
class NeoDebugConfigurationProvider {
    resolveDebugConfiguration(_folder, config, _token) {
        if (!config.type) {
            config.type = 'neo-contract';
            config.request = 'launch';
            config.name = 'Debug Neo contract';
        }
        if (!config.program) {
            return vscode.window
                .showInformationMessage("Set 'program' to the path of your compiled .nef file.")
                .then(() => undefined);
        }
        return config;
    }
}

function activate(context) {
    context.subscriptions.push(
        vscode.debug.registerDebugAdapterDescriptorFactory('neo-contract', new NeoDebugAdapterDescriptorFactory()),
        vscode.debug.registerDebugConfigurationProvider('neo-contract', new NeoDebugConfigurationProvider()));
}

function deactivate() { }

module.exports = { activate, deactivate };
