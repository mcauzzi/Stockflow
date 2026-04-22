declare module 'lz4js' {
  const lz4: {
    decompress(src: Uint8Array, maxSize?: number): Uint8Array;
    decompressBlock(src: Uint8Array, dst: Uint8Array, sOff?: number, sLen?: number, dOff?: number): number;
  };
  export = lz4;
}
