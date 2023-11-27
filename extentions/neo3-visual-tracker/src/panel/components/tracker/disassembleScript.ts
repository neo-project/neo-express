//
// Credit: https://github.com/CityOfZion/neo3-preview/blob/master/src/utils/disassemble.js
//

import * as buffer from "buffer";
import * as cryptoJS from "crypto-js";

const opcodetable: { [opcode: number]: { name: string; size: number } } = {
  0x00: { name: "PUSHINT8", size: 1 },
  0x01: { name: "PUSHINT16", size: 2 },
  0x02: { name: "PUSHINT32", size: 4 },
  0x03: { name: "PUSHINT64", size: 8 },
  0x04: { name: "PUSHINT128", size: 16 },
  0x05: { name: "PUSHINT256", size: 32 },
  0x0a: { name: "PUSHA", size: 4 },
  0x0b: { name: "PUSHNULL", size: 0 },
  0x0c: { name: "PUSHDATA1", size: 1 },
  0x0d: { name: "PUSHDATA2", size: 2 },
  0x0e: { name: "PUSHDATA4", size: 4 },
  0x0f: { name: "PUSHM1", size: 0 },
  0x10: { name: "PUSH0", size: 0 },
  0x11: { name: "PUSH1", size: 0 },
  0x12: { name: "PUSH2", size: 0 },
  0x13: { name: "PUSH3", size: 0 },
  0x14: { name: "PUSH4", size: 0 },
  0x15: { name: "PUSH5", size: 0 },
  0x16: { name: "PUSH6", size: 0 },
  0x17: { name: "PUSH7", size: 0 },
  0x18: { name: "PUSH8", size: 0 },
  0x19: { name: "PUSH9", size: 0 },
  0x1a: { name: "PUSH10", size: 0 },
  0x1b: { name: "PUSH11", size: 0 },
  0x1c: { name: "PUSH12", size: 0 },
  0x1d: { name: "PUSH13", size: 0 },
  0x1e: { name: "PUSH14", size: 0 },
  0x1f: { name: "PUSH15", size: 0 },
  0x20: { name: "PUSH16", size: 0 },
  0x21: { name: "NOP", size: 0 },
  0x22: { name: "JMP", size: 1 },
  0x23: { name: "JMP_L", size: 4 },
  0x24: { name: "JMPIF", size: 1 },
  0x25: { name: "JMPIF_L", size: 4 },
  0x26: { name: "JMPIFNOT", size: 1 },
  0x27: { name: "JMPIFNOT_L", size: 4 },
  0x28: { name: "JMPEQ", size: 1 },
  0x29: { name: "JMPEQ_L", size: 4 },
  0x2a: { name: "JMPNE", size: 1 },
  0x2b: { name: "JMPNE_L", size: 4 },
  0x2c: { name: "JMPGT", size: 1 },
  0x2d: { name: "JMPGT_L", size: 4 },
  0x2e: { name: "JMPGE", size: 1 },
  0x2f: { name: "JMPGE_L", size: 4 },
  0x30: { name: "JMPLT", size: 1 },
  0x31: { name: "JMPLT_L", size: 4 },
  0x32: { name: "JMPLE", size: 1 },
  0x33: { name: "JMPLE_L", size: 4 },
  0x34: { name: "CALL", size: 1 },
  0x35: { name: "CALL_L", size: 4 },
  0x36: { name: "CALLA", size: 0 },
  0x37: { name: "ABORT", size: 0 },
  0x38: { name: "ASSERT", size: 0 },
  0x3a: { name: "THROW", size: 0 },
  0x3b: { name: "TRY", size: 2 },
  0x3c: { name: "TRY_L", size: 8 },
  0x3d: { name: "ENDTRY", size: 1 },
  0x3e: { name: "ENDTRY_L", size: 4 },
  0x3f: { name: "ENDFINALLY", size: 0 },
  0x40: { name: "RET", size: 0 },
  0x41: { name: "SYSCALL", size: 4 },
  0x43: { name: "DEPTH", size: 0 },
  0x45: { name: "DROP", size: 0 },
  0x46: { name: "NIP", size: 0 },
  0x48: { name: "XDROP", size: 0 },
  0x49: { name: "CLEAR", size: 0 },
  0x4a: { name: "DUP", size: 0 },
  0x4b: { name: "OVER", size: 0 },
  0x4d: { name: "PICK", size: 0 },
  0x4e: { name: "TUCK", size: 0 },
  0x50: { name: "SWAP", size: 0 },
  0x51: { name: "ROT", size: 0 },
  0x52: { name: "ROLL", size: 0 },
  0x53: { name: "REVERSE3", size: 0 },
  0x54: { name: "REVERSE4", size: 0 },
  0x55: { name: "REVERSEN", size: 0 },
  0x56: { name: "INITSSLOT", size: 1 },
  0x57: { name: "INITSLOT", size: 2 },
  0x58: { name: "LDSFLD0", size: 0 },
  0x59: { name: "LDSFLD1", size: 0 },
  0x5a: { name: "LDSFLD2", size: 0 },
  0x5b: { name: "LDSFLD3", size: 0 },
  0x5c: { name: "LDSFLD4", size: 0 },
  0x5d: { name: "LDSFLD5", size: 0 },
  0x5e: { name: "LDSFLD6", size: 0 },
  0x5f: { name: "LDSFLD", size: 0 },
  0x60: { name: "STSFLD0", size: 0 },
  0x61: { name: "STSFLD1", size: 0 },
  0x62: { name: "STSFLD2", size: 0 },
  0x63: { name: "STSFLD3", size: 0 },
  0x64: { name: "STSFLD4", size: 0 },
  0x65: { name: "STSFLD5", size: 0 },
  0x66: { name: "STSFLD6", size: 0 },
  0x67: { name: "STSFLD", size: 1 },
  0x68: { name: "LDLOC0", size: 0 },
  0x69: { name: "LDLOC1", size: 0 },
  0x6a: { name: "LDLOC2", size: 0 },
  0x6b: { name: "LDLOC3", size: 0 },
  0x6c: { name: "LDLOC4", size: 0 },
  0x6d: { name: "LDLOC5", size: 0 },
  0x6e: { name: "LDLOC6", size: 0 },
  0x6f: { name: "LDLOC6", size: 1 },
  0x70: { name: "STLOC0", size: 0 },
  0x71: { name: "STLOC1", size: 0 },
  0x72: { name: "STLOC2", size: 0 },
  0x73: { name: "STLOC3", size: 0 },
  0x74: { name: "STLOC4", size: 0 },
  0x75: { name: "STLOC5", size: 0 },
  0x76: { name: "STLOC6", size: 0 },
  0x77: { name: "STLOC7", size: 1 },
  0x78: { name: "LDARG0", size: 0 },
  0x79: { name: "LDARG1", size: 0 },
  0x7a: { name: "LDARG2", size: 0 },
  0x7b: { name: "LDARG3", size: 0 },
  0x7c: { name: "LDARG4", size: 0 },
  0x7d: { name: "LDARG5", size: 0 },
  0x7e: { name: "LDARG6", size: 0 },
  0x7f: { name: "LDARG", size: 1 },
  0x80: { name: "STARG0", size: 0 },
  0x81: { name: "STARG1", size: 0 },
  0x82: { name: "STARG2", size: 0 },
  0x83: { name: "STARG3", size: 0 },
  0x84: { name: "STARG4", size: 0 },
  0x85: { name: "STARG5", size: 0 },
  0x86: { name: "STARG6", size: 0 },
  0x87: { name: "STARG", size: 1 },
  0x88: { name: "NEWBUFFER", size: 0 },
  0x89: { name: "MEMCPY", size: 0 },
  0x8b: { name: "CAT", size: 0 },
  0x8c: { name: "SUBSTR", size: 0 },
  0x8d: { name: "LEFT", size: 0 },
  0x8e: { name: "RIGHT", size: 0 },
  0x90: { name: "INVERT", size: 0 },
  0x91: { name: "AND", size: 0 },
  0x92: { name: "OR", size: 0 },
  0x93: { name: "XOR", size: 0 },
  0x97: { name: "EQUAL", size: 0 },
  0x98: { name: "NOTEQUAL", size: 0 },
  0x99: { name: "SIGN", size: 0 },
  0x9a: { name: "ABS", size: 0 },
  0x9b: { name: "NEGATE", size: 0 },
  0x9c: { name: "INC", size: 0 },
  0x9d: { name: "DEC", size: 0 },
  0x9e: { name: "ADD", size: 0 },
  0x9f: { name: "SUB", size: 0 },
  0xa0: { name: "MUL", size: 0 },
  0xa1: { name: "DIV", size: 0 },
  0xa2: { name: "MOD", size: 0 },
  0xa8: { name: "SHL", size: 0 },
  0xa9: { name: "SHR", size: 0 },
  0xaa: { name: "NOT", size: 0 },
  0xab: { name: "BOOLAND", size: 0 },
  0xac: { name: "BOOLOR", size: 0 },
  0xb1: { name: "NZ", size: 0 },
  0xb3: { name: "NUMEQUAL", size: 0 },
  0xb4: { name: "NUMNOTEQUAL", size: 0 },
  0xb5: { name: "LT", size: 0 },
  0xb6: { name: "LE", size: 0 },
  0xb7: { name: "GT", size: 0 },
  0xb8: { name: "GE", size: 0 },
  0xb9: { name: "MIN", size: 0 },
  0xba: { name: "MAX", size: 0 },
  0xbb: { name: "WITHIN", size: 0 },
  0xc0: { name: "PACK", size: 0 },
  0xc1: { name: "UNPACK", size: 0 },
  0xc2: { name: "NEWARRAY0", size: 0 },
  0xc3: { name: "NEWARRAY", size: 0 },
  0xc4: { name: "NEWARRAY_T", size: 1 },
  0xc5: { name: "NEWSTRUCT0", size: 0 },
  0xc6: { name: "NEWSTRUCT", size: 0 },
  0xc8: { name: "NEWMAP", size: 0 },
  0xca: { name: "SIZE", size: 0 },
  0xcb: { name: "HASKEY", size: 0 },
  0xcc: { name: "KEYS", size: 0 },
  0xcd: { name: "VALUES", size: 0 },
  0xce: { name: "PICKITEM", size: 0 },
  0xcf: { name: "APPEND", size: 0 },
  0xd0: { name: "SETITEM", size: 0 },
  0xd1: { name: "REVERSEITEMS", size: 0 },
  0xd2: { name: "REMOVE", size: 0 },
  0xd3: { name: "CLEARITEMS", size: 0 },
  0xd8: { name: "ISNULL", size: 0 },
  0xd9: { name: "ISTYPE", size: 1 },
  0xdb: { name: "CONVERT", size: 1 },
};
const methodnames = [
  "System.Binary.Serialize",
  "System.Binary.Deserialize",
  "System.Blockchain.GetHeight",
  "System.Blockchain.GetBlock",
  "System.Blockchain.GetTransaction",
  "System.Blockchain.GetTransactionHeight",
  "System.Blockchain.GetTransactionFromBlock",
  "System.Blockchain.GetContract",
  "Neo.Contract.Create",
  "Neo.Contract.Update",
  "System.Contract.Destroy",
  "System.Contract.Call",
  "System.Contract.CallEx",
  "System.Contract.IsStandard",
  "System.Contract.CreateStandardAccount",
  "Neo.Crypto.ECDsaVerify",
  "Neo.Crypto.ECDsaCheckMultiSig",
  "System.Enumerator.Create",
  "System.Enumerator.Next",
  "System.Enumerator.Value",
  "System.Enumerator.Concat",
  "System.Iterator.Create",
  "System.Iterator.Key",
  "System.Iterator.Values",
  "System.Iterator.Concat",
  "System.Json.Serialize",
  "System.Json.Deserialize",
  "Neo.Native.Deploy",
  "System.Runtime.Platform",
  "System.Runtime.GetTrigger",
  "System.Runtime.GetTime",
  "System.Runtime.GetScriptContainer",
  "System.Runtime.GetExecutingScriptHash",
  "System.Runtime.GetCallingScriptHash",
  "System.Runtime.GetEntryScriptHash",
  "System.Runtime.CheckWitness",
  "System.Runtime.GetInvocationCounter",
  "System.Runtime.Log",
  "System.Runtime.Notify",
  "System.Runtime.GetNotifications",
  "System.Runtime.GasLeft",
  "System.Storage.GetContext",
  "System.Storage.GetReadOnlyContext",
  "System.StorageContext.AsReadOnly",
  "System.Storage.Get",
  "System.Storage.Find",
  "System.Storage.Put",
  "System.Storage.PutEx",
  "System.Storage.Delete",
];

// resolve all interop method names to 32-bit hash
const interopmethod: { [key: number]: string } = {};
for (let i = 0; i < methodnames.length; i++) {
  const data = buffer.Buffer.from(methodnames[i], "utf8").toString("hex");
  const datawords = cryptoJS.enc.Hex.parse(data);
  const hash_buffer = buffer.Buffer.from(
    cryptoJS.SHA256(datawords).toString(),
    "hex"
  );
  interopmethod[hash_buffer.readUInt32LE(0)] = methodnames[i];
}

export default function disassembleScript(base64_encoded_script: string) {
  let out = "";
  const script = buffer.Buffer.from(base64_encoded_script, "base64");

  let ip = 0;
  while (ip < script.length) {
    let opcode = script[ip];
    if (opcodetable.hasOwnProperty(opcode)) {
      const opcodedata = opcodetable[opcode];
      let inst = opcodedata.name;

      if (opcodedata.name === "SYSCALL") {
        const hash = script.readUInt32LE(ip + 1);
        let interop_name = interopmethod[hash];
        if (interop_name == null) interop_name = `${hash}`;
        out += `${inst} ${interop_name}\n`;
        ip += 4;
      } else if (opcodedata.size === 0) {
        out += `${inst}\n`;
      } else {
        if (
          inst === "PUSHDATA1" ||
          inst === "PUSHDATA2" ||
          inst === "PUSHDATA4"
        ) {
          let data_size = 0;
          switch (opcodedata.size) {
            case 1: {
              data_size = script.readUInt8(ip + 1);
              break;
            }
            case 2: {
              data_size = script.readUInt16LE(ip + 1);
              break;
            }
            case 4: {
              data_size = script.readUInt32LE(ip + 1);
              break;
            }
            default:
              // if you messed up the size you deserve to pay for it :-)
              out += `SOMEBODY MESSED UP THE PUSHDATA SIZE for ${opcodedata.name} at index ${ip} (size ${opcodedata.size})`;
              return;
          }

          const DATA_START_IDX = ip + opcodedata.size + 1;
          let data = script.slice(DATA_START_IDX, DATA_START_IDX + data_size);
          out += `${inst} ${data.toString("hex")}\n`;
          ip += opcodedata.size + data_size;
        } else {
          let data = script.slice(ip + 1, ip + 1 + opcodedata.size);
          out += `${inst} ${data.toString("hex")}\n`;
          ip += opcodedata.size;
        }
      }
    } else {
      out += `INVALID OPCODE ${opcode.toString()}\n`;
    }
    ip++;
  }
  return out;
}
