import assert from "node:assert/strict";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  SMART_CONTRACT_TEMPLATE_PACKAGE,
  adaptOfficialContractSource,
  instantiateOfficialContract,
} from "./instantiateSmartContractTemplate";

test("adaptOfficialContractSource rewrites namespace and class only", () => {
  const source = `namespace Neo.SmartContract.Template
{
    [DisplayName(nameof(Nep17Contract))]
    [ContractSourceCode("https://github.com/neo-project/neo-devpack-dotnet/tree/master/src/Neo.SmartContract.Template/templates/neocontractnep17/Nep17Contract.cs")]
    public class Nep17Contract : Neo.SmartContract.Framework.Nep17Token
    {
        public delegate void OnSetOwnerDelegate(UInt160 previousOwner, UInt160 newOwner);
    }
}
`;
  const adapted = adaptOfficialContractSource(
    source,
    "TokenEscrowContract",
    "TokenEscrow"
  );
  assert.match(adapted, /namespace TokenEscrow/);
  assert.match(adapted, /class TokenEscrowContract :/);
  assert.match(adapted, /nameof\(TokenEscrowContract\)/);
  assert.match(adapted, /OnSetOwnerDelegate/);
  assert.match(adapted, /neocontractnep17\/Nep17Contract\.cs/);
});

test("instantiateOfficialContract runs dotnet new neocontractnep17", async () => {
  const destination = await mkdtemp(join(tmpdir(), "neoxp-official-"));
  const destFile = join(destination, "src", "TokenEscrowContract.cs");
  try {
    await instantiateOfficialContract({
      shortName: "neocontractnep17",
      className: "TokenEscrowContract",
      contractName: "TokenEscrow",
      destinationSrcFile: destFile,
    });
    const contract = await readFile(destFile, "utf8");
    assert.match(contract, /namespace TokenEscrow/);
    assert.match(
      contract,
      /class TokenEscrowContract : (Neo\.SmartContract\.Framework\.)?Nep17Token/
    );
    assert.match(contract, /EXAMPLE/);
    assert.doesNotMatch(contract, /ChangeNumber/);
  } finally {
    await rm(destination, { recursive: true, force: true });
  }
});

test("template package id is Neo.SmartContract.Template", () => {
  assert.equal(SMART_CONTRACT_TEMPLATE_PACKAGE, "Neo.SmartContract.Template");
});
