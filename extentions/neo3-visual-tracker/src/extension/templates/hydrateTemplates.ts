import * as fs from "fs";

import posixPath from "../util/posixPath";

export function substituteParameters(
  input: string,
  parameters: { [key: string]: string }
): string {
  let result = input;
  for (const key of Object.keys(parameters)) {
    result = result.replaceAll(key, parameters[key]);
  }
  return result;
}

export async function hydrateFiles(
  templatePath: string,
  destinationPath: string,
  parameters: { [key: string]: string }
) {
  await fs.promises.mkdir(destinationPath, { recursive: true });
  const templateFolderContents = await fs.promises.readdir(templatePath);
  for (const item of templateFolderContents) {
    const fullPathToSource = posixPath(templatePath, item);
    const resolvedName = substituteParameters(item, parameters);
    const fullPathToDestination = posixPath(destinationPath, resolvedName);
    const stat = await fs.promises.stat(fullPathToSource);
    if (stat.isDirectory()) {
      await hydrateFiles(
        fullPathToSource,
        posixPath(destinationPath, resolvedName),
        parameters
      );
    } else if (item.endsWith(".template.txt")) {
      const fileContents = (
        await fs.promises.readFile(fullPathToSource)
      ).toString();
      const resolvedContents = substituteParameters(fileContents, parameters);
      await fs.promises.writeFile(
        fullPathToDestination.replace(/\.template\.txt$/, ""),
        resolvedContents
      );
    } else {
      await fs.promises.copyFile(fullPathToSource, fullPathToDestination);
    }
  }
}
