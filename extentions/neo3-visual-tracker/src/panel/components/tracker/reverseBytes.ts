const reverseBytes = (token: string) =>
  token
    .match(/[a-f0-9]{2}/g)
    ?.reverse()
    .join("") || "";

export default reverseBytes;
