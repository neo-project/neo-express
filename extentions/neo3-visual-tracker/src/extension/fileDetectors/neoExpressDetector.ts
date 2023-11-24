import BlockchainIdentifier from "../blockchainIdentifier";
import DetectorBase from "./detectorBase";

const SEARCH_PATTERN = "**/*.neo-express";

export default class NeoExpressDetector extends DetectorBase {
  private blockchainsSnapshot: BlockchainIdentifier[] = [];

  get blockchains(): BlockchainIdentifier[] {
    return [...this.blockchainsSnapshot];
  }

  constructor(private readonly extensionPath: string) {
    super(SEARCH_PATTERN);
  }

  async processFiles() {
    this.blockchainsSnapshot = (
      await Promise.all(
        this.files.map((_) =>
          BlockchainIdentifier.fromNeoExpressConfig(this.extensionPath, _)
        )
      )
    ).filter((_) => !!_) as BlockchainIdentifier[];
  }
}
