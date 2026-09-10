import * as fs from "fs";
import * as vscode from "vscode";

import IoHelpers from "../util/ioHelpers";
import JSONC from "../util/JSONC";
import {
  csharpStarterLabels,
  findCsharpStarter,
  CsharpStarter,
} from "./csharpStarters";
import { hydrateFiles } from "./hydrateTemplates";
import { Language, languages } from "./languages";
import posixPath from "../util/posixPath";
import workspaceFolder from "../util/workspaceFolder";

export default class Templates {
  static async newContract(context: vscode.ExtensionContext) {
    const rootFolder = workspaceFolder();
    if (!rootFolder) {
      vscode.window.showErrorMessage(
        "Please open a folder in your Visual Studio Code workspace before creating a contract"
      );
      return;
    }

    const languageLabels: { [code: string]: string } = {
      csharp: "C# (recommended for N3)",
      python: "Python (neo3-boa)",
      java: "Java (neow3j)",
    };
    const languageChoices = Object.keys(languages).map(
      (code) => languageLabels[code] || code
    );
    const languageChoice = await IoHelpers.multipleChoice(
      "Language for the new contract",
      ...languageChoices
    );
    if (!languageChoice) {
      return;
    }
    const languageCode =
      Object.keys(languageLabels).find(
        (code) => languageLabels[code] === languageChoice
      ) || languageChoice;
    const language = languages[languageCode];
    if (!language) {
      return;
    }

    let csharpStarter: CsharpStarter | undefined;
    if (languageCode === "csharp") {
      const starterChoice = await IoHelpers.multipleChoice(
        "Contract template",
        ...csharpStarterLabels()
      );
      if (!starterChoice) {
        return;
      }
      csharpStarter = findCsharpStarter(starterChoice);
      if (!csharpStarter) {
        return;
      }
    }

    const parameters = await Templates.gatherParameters(language);
    if (!parameters) {
      return;
    }

    const contractName = parameters["$_CONTRACTNAME_$"];
    const contractPath = posixPath(rootFolder, "contracts", contractName);
    const templatePath = posixPath(
      context.extensionPath,
      "resources",
      "new-contract",
      languageCode
    );
    if (fs.existsSync(contractPath)) {
      vscode.window.showErrorMessage(
        `A contract called ${contractName} already exists in this vscode.workspace.`
      );
      return;
    }

    await hydrateFiles(templatePath, contractPath, parameters);
    if (csharpStarter?.overlay) {
      await hydrateFiles(
        posixPath(
          context.extensionPath,
          "resources",
          "new-contract",
          "csharp-starters",
          csharpStarter.id
        ),
        contractPath,
        parameters
      );
    }

    const starterNote = csharpStarter
      ? ` (${csharpStarter.label})`
      : "";
    vscode.window.showInformationMessage(
      `Created ${contractName}${starterNote} under contracts/${contractName}. Build it, then deploy from Smart contracts.`
    );

    const mainFile = parameters["$_MAINFILE_$"];
    if (mainFile) {
      await vscode.window.showTextDocument(
        await vscode.workspace.openTextDocument(
          posixPath(contractPath, mainFile)
        )
      );
    }

    switch (languageCode) {
      // neo3-boa needs the name of the contract to compile
      case "python":
        if (language.tasks) {
          language.tasks.forEach(task => {
            if (task.args) {
              task.args.forEach((element, index) => { 
                task.args[index] = element.replace("$_CONTRACTNAME_$", contractName)
              })
            }
          })
        }
        break;
    }

    const dotVsCodeFolderPath = posixPath(rootFolder, ".vscode");
    if (language.settings || language.tasks || language.extensions) {
      try {
        await fs.promises.mkdir(dotVsCodeFolderPath);
      } catch {}
    }

    if (language.extensions) {
      const extensionsJsonPath = posixPath(
        dotVsCodeFolderPath,
        "extensions.json"
      );
      let extensionsJson: { recommendations: string[] } = {
        recommendations: [],
      };
      try {
        const extensionsJsonTxt = (
          await fs.promises.readFile(extensionsJsonPath)
        ).toString();
        extensionsJson = JSONC.parse(extensionsJsonTxt);
        if (
          !extensionsJson.recommendations ||
          !Array.isArray(extensionsJson.recommendations)
        ) {
          extensionsJson.recommendations = [];
        }
      } catch {}
      for (const extension of language.extensions) {
        if (extensionsJson.recommendations.indexOf(extension) === -1) {
          extensionsJson.recommendations.push(extension);
        }
      }
      await fs.promises.writeFile(
        extensionsJsonPath,
        JSONC.stringify(extensionsJson)
      );
    }

    if (language.settings) {
      const settingsJsonPath = posixPath(dotVsCodeFolderPath, "settings.json");
      let settingsJson: { [settingName: string]: string } = {};
      try {
        const settingsJsonTxt = (
          await fs.promises.readFile(settingsJsonPath)
        ).toString();
        settingsJson = JSONC.parse(settingsJsonTxt);
      } catch {}
      for (const settingName of Object.keys(language.settings)) {
        settingsJson[settingName] = language.settings[settingName];
      }
      await fs.promises.writeFile(
        settingsJsonPath,
        JSONC.stringify(settingsJson)
      );
    }

    if (language.tasks) {
      const tasksJsonPath = posixPath(dotVsCodeFolderPath, "tasks.json");
      let tasksJson: { version: string; tasks: any } = {
        version: "2.0.0",
        tasks: [],
      };
      try {
        const tasksJsonTxt = (
          await fs.promises.readFile(tasksJsonPath)
        ).toString();
        tasksJson = JSONC.parse(tasksJsonTxt);
        if (!tasksJson.tasks || !Array.isArray(tasksJson.tasks)) {
          tasksJson.tasks = [];
        }
      } catch {}
      let autorunTaskLabels: string[] = [];
      for (const task of language.tasks) {
        const taskJson = {
          options: {
            cwd: "${workspaceFolder}/contracts/" + contractName,
          },
          label: `${contractName}: ${task.label}`,
          command: task.command,
          type: task.type,
          args: task.args,
          group: task.group,
          presentation: { reveal: "silent" },
          problemMatcher: task.problemMatcher,
          dependsOn: task.dependsOnLabel
            ? `${contractName}: ${task.dependsOnLabel}`
            : undefined,
        };
        (tasksJson.tasks as any[]).push(taskJson);
        if (task.autoRun) {
          autorunTaskLabels.push(taskJson.label);
        }
      }
      await fs.promises.writeFile(tasksJsonPath, JSONC.stringify(tasksJson));
      // TODO: Investigate ways to remove this sleep hack
      await new Promise((resolve) => setTimeout(resolve, 1000));
      const tasks = await vscode.tasks.fetchTasks();
      const buildTasks = tasks.filter(
        (_) => autorunTaskLabels.indexOf(_.name) !== -1
      );
      for (const buildTask of buildTasks) {
        vscode.tasks.executeTask(buildTask);
      }
    }
  }

  private static async gatherParameters(
    language: Language
  ): Promise<{ [key: string]: string } | undefined> {
    const result: { [key: string]: string } = {};
    if (language.variables) {
      for (const variableName of Object.keys(language.variables)) {
        const variable = language.variables[variableName];
        let value: string | undefined = "";
        if (variable.eval) {
          value = await variable.eval(result);
        }
        if (variable.prompt) {
          value = await IoHelpers.enterString(variable.prompt, value);
        }
        if (variable.parse) {
          value = await variable.parse(value);
        }
        if (!value) {
          // All variables are considered required
          return undefined;
        }
        result[`$_${variableName}_$`] = value;
      }
    }
    return result;
  }
}
