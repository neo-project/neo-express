export const isEmptyArgument = (value: unknown) =>
  value === null || value === undefined || value === "";

export const normalizeArguments = <T>(
  args: T[],
  requiredArgumentCount: number
) => {
  const normalizedArguments: Array<T | string> = [...args];
  while (
    normalizedArguments.length > requiredArgumentCount &&
    isEmptyArgument(normalizedArguments[normalizedArguments.length - 1])
  ) {
    normalizedArguments.pop();
  }
  while (normalizedArguments.length < requiredArgumentCount) {
    normalizedArguments.push("");
  }
  return normalizedArguments;
};

export const valueToString = (value: unknown) => {
  if (value === null || value === undefined) {
    return "";
  } else if (Array.isArray(value) || typeof value === "object") {
    return JSON.stringify(value);
  } else {
    return `${value}`;
  }
};

export const stringToValue = (text: string) => {
  if (`${parseInt(text)}` === text) {
    return parseInt(text);
  } else if (`${parseFloat(text)}` === text) {
    return parseFloat(text);
  } else {
    try {
      return JSON.parse(text);
    } catch {
      return text;
    }
  }
};

export const isSaveShortcut = ({
  ctrlKey,
  key,
  metaKey,
}: {
  ctrlKey: boolean;
  key: string;
  metaKey: boolean;
}) => (metaKey || ctrlKey) && key.toLowerCase() === "s";
