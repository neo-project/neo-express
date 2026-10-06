import { spawn } from "node:child_process";
import { mkdir, mkdtemp, readdir, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";

export const SMART_CONTRACT_TEMPLATE_PACKAGE = "Neo.SmartContract.Template";
export const SMART_CONTRACT_TEMPLATE_VERSION = "3.10.1";

let templateInstalled = false;

function runDotnet(
  args: string[]
): Promise<{ status: number; stdout: string; stderr: string }> {
  return new Promise((resolve, reject) => {
    const child = spawn("dotnet", args, { windowsHide: true });
    let stdout = "";
    let stderr = "";
    child.stdout?.on("data", (chunk) => {
      stdout += chunk.toString();
    });
    child.stderr?.on("data", (chunk) => {
      stderr += chunk.toString();
    });
    child.on("error", reject);
    child.on("close", (code) => {
      resolve({ status: code ?? 1, stdout, stderr });
    });
  });
}

export function adaptOfficialContractSource(
  source: string,
  className: string,
  contractName: string
): string {
  const classMatch = source.match(/\bclass\s+(\w+)/);
  const oldClass = classMatch?.[1] ?? className;
  return source
    .replace(/namespace\s+[\w.]+/, `namespace ${contractName}`)
    .replace(
      new RegExp(`nameof\\(${oldClass}\\)`, "g"),
      `nameof(${className})`
    )
    .replace(new RegExp(`\\bclass\\s+${oldClass}\\b`), `class ${className}`);
}

async function findContractSources(root: string): Promise<string[]> {
  const results: string[] = [];
  async function walk(dir: string) {
    const entries = await readdir(dir, { withFileTypes: true });
    for (const entry of entries) {
      const full = join(dir, entry.name);
      if (entry.isDirectory()) {
        if (
          entry.name === "obj" ||
          entry.name === "bin" ||
          entry.name === ".template.config" ||
          /unittests/i.test(entry.name)
        ) {
          continue;
        }
        await walk(full);
        continue;
      }
      if (entry.name.endsWith(".cs") && !entry.name.endsWith("AssemblyInfo.cs")) {
        results.push(full);
      }
    }
  }
  await walk(root);
  return results;
}

export async function ensureSmartContractTemplateInstalled(): Promise<void> {
  if (templateInstalled) {
    return;
  }
  const listed = await runDotnet(["new", "list"]);
  const output = `${listed.stdout}\n${listed.stderr}`;
  if (listed.status === 0 && /\bneocontractnep17\b/.test(output)) {
    templateInstalled = true;
    return;
  }
  const install = await runDotnet([
    "new",
    "install",
    `${SMART_CONTRACT_TEMPLATE_PACKAGE}@${SMART_CONTRACT_TEMPLATE_VERSION}`,
  ]);
  if (install.status !== 0) {
    throw new Error(
      `dotnet new install ${SMART_CONTRACT_TEMPLATE_PACKAGE} failed:\n${install.stdout}\n${install.stderr}`
    );
  }
  templateInstalled = true;
}

export async function instantiateOfficialContract(options: {
  shortName: string;
  className: string;
  contractName: string;
  destinationSrcFile: string;
}): Promise<void> {
  await ensureSmartContractTemplateInstalled();
  const temp = await mkdtemp(join(tmpdir(), "neoxp-dotnet-new-"));
  try {
    const created = await runDotnet([
      "new",
      options.shortName,
      "-n",
      options.className,
      "-o",
      temp,
      "--force",
    ]);
    if (created.status !== 0) {
      throw new Error(
        `dotnet new ${options.shortName} failed:\n${created.stdout}\n${created.stderr}`
      );
    }
    const sources = await findContractSources(temp);
    if (sources.length === 0) {
      throw new Error(
        `dotnet new ${options.shortName} produced no contract source`
      );
    }
    const preferred =
      sources.find((path) => path.endsWith(`${options.className}.cs`)) ??
      sources[0];
    const adapted = adaptOfficialContractSource(
      await readFile(preferred, "utf8"),
      options.className,
      options.contractName
    );
    await mkdir(dirname(options.destinationSrcFile), { recursive: true });
    await writeFile(options.destinationSrcFile, adapted);
  } finally {
    await rm(temp, { recursive: true, force: true });
  }
}
