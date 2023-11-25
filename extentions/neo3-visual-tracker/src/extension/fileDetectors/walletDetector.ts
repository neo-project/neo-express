import DetectorBase from "./detectorBase";
import Wallet from "../wallet";

const SEARCH_PATTERN = "**/*.json";

export default class WalletDetector extends DetectorBase {
  private walletsSnapshot: Wallet[] = [];

  public get wallets() {
    return [...this.walletsSnapshot];
  }

  constructor() {
    super(SEARCH_PATTERN);
  }

  async processFiles() {
    this.walletsSnapshot = (
      await Promise.all(this.files.map((_) => Wallet.fromJsonFile(_)))
    ).filter((_) => !!_) as Wallet[];
  }
}
