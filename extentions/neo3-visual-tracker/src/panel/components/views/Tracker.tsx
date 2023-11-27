import React from "react";

import AddressDetails from "../tracker/AddressDetails";
import BlockDetails from "../tracker/BlockDetails";
import BlockList from "../tracker/BlockList";
import BlockNavigation from "../tracker/BlockNavigation";
import Dialog from "../Dialog";
import Search from "../tracker/Search";
import TrackerViewRequest from "../../../shared/messages/trackerViewRequest";
import TrackerViewState from "../../../shared/viewState/trackerViewState";
import TransactionDetails from "../tracker/TransactionDetails";

type Props = {
  viewState: TrackerViewState;
  postMessage: (message: TrackerViewRequest) => void;
};

export default function Tracker({ viewState, postMessage }: Props) {
  return (
    <>
      {!!viewState.selectedBlock && (
        <Dialog
          affinity="top-left"
          title={`Block ${viewState.selectedBlock.index}`}
          onClose={() => postMessage({ selectBlock: "" })}
        >
          <BlockDetails
            autoCompleteData={viewState.autoCompleteData}
            block={viewState.selectedBlock}
            selectedTransactionHash={viewState.selectedTransaction?.tx.hash}
            selectAddress={(selectAddress) => postMessage({ selectAddress })}
            selectTransaction={(txid) =>
              postMessage({ selectTransaction: txid })
            }
          />
        </Dialog>
      )}
      {!!viewState.selectedTransaction && (
        <Dialog
          affinity="middle"
          title={`Transaction`}
          onClose={() => postMessage({ selectTransaction: "" })}
        >
          <TransactionDetails
            applicationLog={viewState.selectedTransaction.log}
            autoCompleteData={viewState.autoCompleteData}
            transaction={viewState.selectedTransaction.tx}
            selectAddress={(address) => postMessage({ selectAddress: address })}
          />
        </Dialog>
      )}
      {!!viewState.selectedAddress && (
        <Dialog
          affinity="bottom-right"
          onClose={() => postMessage({ selectAddress: "" })}
        >
          <AddressDetails
            addressInfo={viewState.selectedAddress}
            autoCompleteData={viewState.autoCompleteData}
          />
        </Dialog>
      )}
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          alignItems: "stretch",
          alignContent: "stretch",
          height: "100%",
        }}
      >
        <Search
          searchHistory={viewState.searchHistory}
          onSearch={(query) => postMessage({ search: query })}
        />
        <div
          style={{ flex: "none 1", overflow: "hidden", position: "relative" }}
        >
          <div style={{ minHeight: "100vh" }}>
            <BlockList
              blocks={viewState.blocks}
              populatedBlocksFilterEnabled={
                viewState.populatedBlocksFilterEnabled
              }
              populatedBlocksFilterSupported={
                viewState.populatedBlocksFilterSupported
              }
              selectedBlock={viewState.selectedBlock}
              selectBlock={(hash) => postMessage({ selectBlock: hash })}
              togglePopulatedBlocksFilter={(enabled) =>
                postMessage({ togglePopulatedBlockFilter: { enabled } })
              }
            />
          </div>
        </div>
        <BlockNavigation
          style={{ margin: 10, textAlign: "center" }}
          blocks={viewState.blocks}
          blockHeight={viewState.blockHeight}
          paginationDistance={viewState.paginationDistance}
          startAtBlock={viewState.startAtBlock}
          setStartAtBlock={(setStartAtBlock) =>
            postMessage({ setStartAtBlock })
          }
        />
      </div>
    </>
  );
}
