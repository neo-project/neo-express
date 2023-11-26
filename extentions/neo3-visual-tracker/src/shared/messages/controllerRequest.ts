import ViewStateBase from "../viewState/viewStateBase";

type ControllerRequest = {
  loadingState?: { isLoading: boolean };
  viewState?: ViewStateBase;
};

export default ControllerRequest;
