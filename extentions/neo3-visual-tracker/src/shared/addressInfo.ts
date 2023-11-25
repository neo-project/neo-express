type AddressInfo = {
  address: string;
  neoBalance: number;
  gasBalance: number;
  allBalances: { [assetHash: string]: number };
};

export default AddressInfo;
