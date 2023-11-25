import BlockchainIdentifier from "../blockchainIdentifier";

//
// Represents all possible arguments to any command explosed by
// the extension.
//
// All values are optional, and will be solicited from the user by
// the command implementation if required but undefined.
//
// Example of how to construct a command URI:
//   const args = [{ asset: "NEO", amount: 1000, sender: "alice", receiver: "bob" }];
//   const commandUri = vscode.Uri.parse(
//     `command:neo3-visual-devtracker.express.transfer?${
//       encodeURIComponent(JSON.stringify(args))
//     }`
//   );
//
type CommandArguments = {
  address?: string;
  amount?: number;
  asset?: string;
  blockchainIdentifier?: BlockchainIdentifier;
  hash?: string;
  path?: string;
  receiver?: string;
  secondsPerBlock?: number;
  sender?: string;
};

async function sanitizeCommandArguments(input: any): Promise<CommandArguments> {
  return {
    address: input.address
      ? `${input.address}`.replace(/[^a-z0-9]/gi, "")
      : undefined,
    amount: parseFloat(input.amount) || undefined,
    asset: input.asset
      ? `${input.asset}`.replace(/[^a-z0-9]/gi, "")
      : undefined,
    blockchainIdentifier: undefined, // TODO: Allow blockchain to be specified in command URIs
    hash: input.hash ? `${input.hash}`.replace(/[^xa-f0-9]/gi, "") : undefined,
    path: undefined, // TODO: Allow specification of path in command URIs (ensure supplied path is within the workspace though)
    receiver: input.receiver
      ? `${input.receiver}`.replace(/[^a-z0-9]/gi, "")
      : undefined,
    secondsPerBlock: parseInt(input.secondsPerBlock) || undefined,
    sender: input.sender
      ? `${input.sender}`.replace(/[^a-z0-9]/gi, "")
      : undefined,
  };
}

export { CommandArguments, sanitizeCommandArguments };
