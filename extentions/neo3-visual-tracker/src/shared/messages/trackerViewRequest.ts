type TrackerViewRequest = {
  selectAddress?: string;
  selectBlock?: string;
  selectTransaction?: string;
  setStartAtBlock?: number;
  search?: string;
  togglePopulatedBlockFilter?: { enabled: boolean };
};

export default TrackerViewRequest;
