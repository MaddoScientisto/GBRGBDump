export const DEFAULT_BAUD_RATE = 1_000_000;
export const FAST_BAUD_RATE = 1_700_000;
export const BLOCK_SIZE = 64;
export const IMAGE_SIZE = 3_584;
export const IMAGE_BLOCK_COUNT = IMAGE_SIZE / BLOCK_SIZE;
export const METADATA_SIZE = 2_560;
export const METADATA_BLOCK_COUNT = METADATA_SIZE / BLOCK_SIZE;
export const LAST_ADDRESS_SCAN_BYTES = 2_500;
export const BLOCK_TIMEOUT_MS = 500;

const LAST_ADDRESS_MAP = new Map<number, number>([
  [0x00, 8],
  [0x01, 7],
  [0x03, 6],
  [0x07, 5],
  [0x0f, 4],
  [0x1f, 3],
  [0x3f, 2],
  [0x7f, 1],
]);

export interface PicNRecConnectOptions {
  baudRate?: number;
  fastMode?: boolean;
}

export interface PicNRecReadImageOptions {
  retries?: number;
  retryDelayMs?: number;
  onAttempt?: (attempt: number) => void;
}

export interface PicNRecClientOptions {
  logger?: (message: string) => void;
}

type PendingWaiter = {
  resolve: () => void;
  reject: (error: unknown) => void;
};

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => {
    window.setTimeout(resolve, ms);
  });
}

function makeTimeoutError(message: string): Error {
  const error = new Error(message);
  error.name = "TimeoutError";
  return error;
}

function printableAscii(byte: number): string {
  return byte >= 32 && byte <= 126 ? String.fromCharCode(byte) : ".";
}

export function decodeLastImageNumber(metadata: Uint8Array): number {
  let lastAddress = 0;

  for (let index = 0; index < Math.min(metadata.length, LAST_ADDRESS_SCAN_BYTES); index += 1) {
    lastAddress += LAST_ADDRESS_MAP.get(metadata[index]) ?? 0;
  }

  return lastAddress;
}

export function formatHexDump(bytes: Uint8Array, bytesPerRow = 16): string {
  if (!(bytes instanceof Uint8Array) || bytes.length === 0) {
    return "";
  }

  const addressWidth = Math.max(4, Math.ceil(Math.log2(bytes.length || 1) / 4));
  const rows: string[] = [];

  for (let offset = 0; offset < bytes.length; offset += bytesPerRow) {
    const slice = bytes.subarray(offset, offset + bytesPerRow);
    const hex = Array.from(slice, (value) => value.toString(16).padStart(2, "0")).join(" ");
    const ascii = Array.from(slice, printableAscii).join("");
    rows.push(`${offset.toString(16).padStart(addressWidth, "0")}  ${hex.padEnd(bytesPerRow * 3 - 1, " ")}  ${ascii}`);
  }

  return rows.join("\n");
}

export function createSavImageBuffer(bytes: Uint8Array): Uint8Array {
  if (!(bytes instanceof Uint8Array)) {
    throw new TypeError("bytes must be a Uint8Array");
  }

  if (bytes.length !== IMAGE_SIZE) {
    throw new Error(`Expected ${IMAGE_SIZE} bytes for a PicNRec .sav image, received ${bytes.length}.`);
  }

  return bytes.slice();
}

export class PicNRecWebSerialClient {
  private readonly logger: (message: string) => void;
  private readonly encoder = new TextEncoder();
  private port: SerialPort | null = null;
  private reader: ReadableStreamDefaultReader<Uint8Array> | null = null;
  private writer: WritableStreamDefaultWriter<Uint8Array> | null = null;
  private currentBaudRateValue: number | null = null;
  private readLoopTask: Promise<void> | null = null;
  private readLoopActive = false;
  private readLoopError: unknown = null;
  private bufferedChunks: Uint8Array[] = [];
  private bufferedBytes = 0;
  private waiters: PendingWaiter[] = [];

  public constructor(options: PicNRecClientOptions = {}) {
    this.logger = typeof options.logger === "function" ? options.logger : () => undefined;
  }

  public static isSupported(): boolean {
    return typeof navigator !== "undefined" && "serial" in navigator;
  }

  public get currentBaudRate(): number | null {
    return this.currentBaudRateValue;
  }

  public get isConnected(): boolean {
    return Boolean(this.port && this.reader && this.writer);
  }

  public getPortSummary(): string {
    if (!this.port) {
      return "No serial port selected.";
    }

    const info = this.port.getInfo?.() ?? {};
    const vendorId = info.usbVendorId ? `0x${info.usbVendorId.toString(16)}` : "unknown";
    const productId = info.usbProductId ? `0x${info.usbProductId.toString(16)}` : "unknown";
    return `USB VID ${vendorId}, PID ${productId}`;
  }

  public async requestPort(options: SerialPortRequestOptions = {}): Promise<SerialPort> {
    if (!PicNRecWebSerialClient.isSupported()) {
      throw new Error("Web Serial is not available in this browser.");
    }

    this.port = await navigator.serial.requestPort(options);
    this.logger(`Selected port: ${this.getPortSummary()}`);
    return this.port;
  }

  public async connect(options: PicNRecConnectOptions = {}): Promise<void> {
    if (!this.port) {
      await this.requestPort();
    }

    await this.openPort(options.baudRate ?? DEFAULT_BAUD_RATE);
    await this.flushInput();

    if (options.fastMode) {
      await this.enterFastMode();
    }

    this.logger(`Connected at ${this.currentBaudRateValue} baud.`);
  }

  public async disconnect(): Promise<void> {
    await this.closePort();
    this.logger("Disconnected.");
  }

  public async enterFastMode(): Promise<void> {
    this.ensureConnected();
    await this.flushInput();
    await this.writeAscii(">", "Switch device UART to fast mode");
    await sleep(100);

    try {
      await this.reopenPort(FAST_BAUD_RATE);
      this.logger(`Fast mode active at ${FAST_BAUD_RATE} baud.`);
    } catch (error) {
      this.logger(`Fast mode reopen failed, falling back to ${DEFAULT_BAUD_RATE}: ${(error as Error).message}`);
      await this.reopenPort(DEFAULT_BAUD_RATE);
    }

    await this.flushInput();
  }

  public async readLastImageNumber(): Promise<number> {
    const metadata = await this.readMetadata();
    const lastImageNumber = decodeLastImageNumber(metadata);
    this.logger(`Decoded last image number: ${lastImageNumber}`);
    return lastImageNumber;
  }

  public async readMetadata(): Promise<Uint8Array> {
    this.ensureConnected();
    await this.flushInput();
    await this.setNumber("A", 0);
    await this.setMode("R");

    try {
      const metadata = await this.readBlockSequence(METADATA_BLOCK_COUNT);
      await this.setMode("0");
      return metadata;
    } catch (error) {
      await this.safeStop();
      throw error;
    }
  }

  public async readImage(imageNumber: number, options: PicNRecReadImageOptions = {}): Promise<Uint8Array> {
    this.ensureConnected();

    const retries = options.retries ?? 3;
    const retryDelayMs = options.retryDelayMs ?? 200;

    for (let attempt = 1; attempt <= retries; attempt += 1) {
      options.onAttempt?.(attempt);

      try {
        await this.flushInput();
        await this.setNumber("A", imageNumber);
        await this.setMode("R");
        const image = await this.readBlockSequence(IMAGE_BLOCK_COUNT);
        await this.setMode("0");
        this.logger(`Read image ${imageNumber} on attempt ${attempt}.`);
        return image;
      } catch (error) {
        await this.safeStop();
        await this.flushInput();

        if (attempt === retries) {
          throw new Error(`Image read failed after ${retries} attempts: ${(error as Error).message}`);
        }

        this.logger(`Attempt ${attempt} failed: ${(error as Error).message}`);
        await sleep(retryDelayMs);
      }
    }

    throw new Error("Image read failed for an unknown reason.");
  }

  public async clearLastImageMetadata(): Promise<void> {
    this.ensureConnected();
    await this.flushInput();
    await this.writeAscii("k", "Clear metadata block");
    const ack = await this.readExact(1, BLOCK_TIMEOUT_MS);

    if (ack[0] !== 0x31) {
      throw new Error(`Unexpected clear acknowledgement: 0x${ack[0].toString(16).padStart(2, "0")}`);
    }

    this.logger("Metadata block clear acknowledged.");
  }

  public async flushInput(silenceMs = 40, maxDrainMs = 300): Promise<void> {
    this.clearBufferedData();
    const deadline = Date.now() + maxDrainMs;

    while (Date.now() < deadline) {
      try {
        await this.waitForSignal(silenceMs);
        this.clearBufferedData();
      } catch (error) {
        if ((error as Error).name === "TimeoutError") {
          return;
        }

        throw error;
      }
    }
  }

  private async openPort(baudRate: number): Promise<void> {
    if (!this.port) {
      throw new Error("No port selected.");
    }

    if (this.port.readable || this.port.writable) {
      await this.closePort();
    }

    await this.port.open({
      baudRate,
      dataBits: 8,
      stopBits: 1,
      parity: "none",
      bufferSize: 4096,
    });

    try {
      await this.port.setSignals?.({ dataTerminalReady: true, requestToSend: true });
    } catch (error) {
      this.logger(`Signal setup skipped: ${(error as Error).message}`);
    }

    this.currentBaudRateValue = baudRate;
    this.reader = this.port.readable!.getReader();
    this.writer = this.port.writable!.getWriter();
    this.startReadLoop();
  }

  private async reopenPort(baudRate: number): Promise<void> {
    const selectedPort = this.port;
    if (!selectedPort) {
      throw new Error("No port selected.");
    }

    await this.closePort(true);
    await sleep(100);
    this.port = selectedPort;
    await this.openPort(baudRate);
  }

  private async closePort(keepSelection = false): Promise<void> {
    this.readLoopActive = false;
    this.rejectWaiters(new Error("Serial port closed."));

    if (this.reader) {
      try {
        await this.reader.cancel();
      } catch {
      }
    }

    if (this.readLoopTask) {
      try {
        await this.readLoopTask;
      } catch {
      }
    }

    if (this.reader) {
      this.reader.releaseLock();
      this.reader = null;
    }

    if (this.writer) {
      this.writer.releaseLock();
      this.writer = null;
    }

    this.readLoopTask = null;
    this.readLoopError = null;
    this.clearBufferedData();

    if (this.port?.readable) {
      await this.port.close();
    }

    this.currentBaudRateValue = null;

    if (!keepSelection) {
      this.port = null;
    }
  }

  private startReadLoop(): void {
    this.readLoopError = null;
    this.readLoopActive = true;
    this.readLoopTask = (async () => {
      try {
        while (this.readLoopActive && this.reader) {
          const { value, done } = await this.reader.read();
          if (done) {
            break;
          }

          if (value && value.length > 0) {
            this.bufferedChunks.push(value);
            this.bufferedBytes += value.length;
            this.wakeWaiters();
          }
        }
      } catch (error) {
        this.readLoopError = error;
        this.rejectWaiters(error);
      } finally {
        this.readLoopActive = false;
        this.wakeWaiters();
      }
    })();
  }

  private async setMode(command: string): Promise<void> {
    await this.writeAscii(command, `Send mode command ${command}`);
  }

  private async setNumber(command: string, number: number): Promise<void> {
    const hex = Math.max(0, Number(number)).toString(16);
    await this.writeAscii(`${command}${hex}\0`, `Send numeric command ${command}${hex}`);
  }

  private async writeAscii(text: string, label: string): Promise<void> {
    this.ensureConnected();
    this.logger(label);
    await this.writer!.write(this.encoder.encode(text));
  }

  private async readBlockSequence(blockCount: number): Promise<Uint8Array> {
    const output = new Uint8Array(blockCount * BLOCK_SIZE);
    let offset = 0;

    for (let blockIndex = 0; blockIndex < blockCount; blockIndex += 1) {
      const block = await this.readExact(BLOCK_SIZE, BLOCK_TIMEOUT_MS);
      output.set(block, offset);
      offset += block.length;

      if (blockIndex < blockCount - 1) {
        await this.setMode("1");
      }
    }

    return output;
  }

  private async readExact(length: number, timeoutMs: number): Promise<Uint8Array> {
    const deadline = Date.now() + timeoutMs;
    let lastObservedBytes = this.bufferedBytes;

    while (this.bufferedBytes < length) {
      if (this.readLoopError) {
        throw this.readLoopError;
      }

      if (!this.readLoopActive) {
        throw new Error(`Serial read stopped with ${this.bufferedBytes} of ${length} bytes buffered.`);
      }

      const remainingMs = deadline - Date.now();
      if (remainingMs <= 0) {
        throw makeTimeoutError(`Timed out waiting for ${length} bytes; received ${this.bufferedBytes}.`);
      }

      await this.waitForBufferGrowth(lastObservedBytes, remainingMs);
      lastObservedBytes = this.bufferedBytes;
    }

    return this.consume(length);
  }

  private async waitForBufferGrowth(previousBufferedBytes: number, timeoutMs: number): Promise<void> {
    if (this.bufferedBytes > previousBufferedBytes) {
      return;
    }

    if (this.readLoopError) {
      throw this.readLoopError;
    }

    if (!this.readLoopActive) {
      throw new Error("Serial read loop is not running.");
    }

    await new Promise<void>((resolve, reject) => {
      const waiter: PendingWaiter = {
        resolve: () => {
          cleanup();
          resolve();
        },
        reject: (error) => {
          cleanup();
          reject(error);
        },
      };

      const timeoutHandle = window.setTimeout(() => {
        waiter.reject(makeTimeoutError(`Timed out waiting for more serial data after ${previousBufferedBytes} bytes.`));
      }, timeoutMs);

      const cleanup = () => {
        window.clearTimeout(timeoutHandle);
        this.waiters = this.waiters.filter((candidate) => candidate !== waiter);
      };

      this.waiters.push(waiter);
    });
  }

  private consume(length: number): Uint8Array {
    const target = new Uint8Array(length);
    let written = 0;

    while (written < length) {
      const chunk = this.bufferedChunks[0];
      const remaining = length - written;

      if (chunk.length <= remaining) {
        target.set(chunk, written);
        written += chunk.length;
        this.bufferedChunks.shift();
      } else {
        target.set(chunk.subarray(0, remaining), written);
        this.bufferedChunks[0] = chunk.subarray(remaining);
        written += remaining;
      }
    }

    this.bufferedBytes -= length;
    return target;
  }

  private async waitForSignal(timeoutMs: number): Promise<void> {
    if (this.bufferedBytes > 0) {
      return;
    }

    if (this.readLoopError) {
      throw this.readLoopError;
    }

    if (!this.readLoopActive) {
      throw new Error("Serial read loop is not running.");
    }

    await new Promise<void>((resolve, reject) => {
      const waiter: PendingWaiter = {
        resolve: () => {
          cleanup();
          resolve();
        },
        reject: (error) => {
          cleanup();
          reject(error);
        },
      };

      const timeoutHandle = window.setTimeout(() => {
        waiter.reject(makeTimeoutError(`Timed out waiting for ${BLOCK_SIZE} byte block data.`));
      }, timeoutMs);

      const cleanup = () => {
        window.clearTimeout(timeoutHandle);
        this.waiters = this.waiters.filter((candidate) => candidate !== waiter);
      };

      this.waiters.push(waiter);
    });
  }

  private wakeWaiters(): void {
    for (const waiter of [...this.waiters]) {
      waiter.resolve();
    }
  }

  private rejectWaiters(error: unknown): void {
    for (const waiter of [...this.waiters]) {
      waiter.reject(error);
    }
  }

  private clearBufferedData(): void {
    this.bufferedChunks = [];
    this.bufferedBytes = 0;
  }

  private ensureConnected(): void {
    if (!this.isConnected) {
      throw new Error("Serial device is not connected.");
    }
  }

  private async safeStop(): Promise<void> {
    if (!this.isConnected) {
      return;
    }

    try {
      await this.setMode("0");
    } catch {
    }
  }
}

export const protocolConstants = {
  DEFAULT_BAUD_RATE,
  FAST_BAUD_RATE,
  BLOCK_SIZE,
  IMAGE_SIZE,
  METADATA_SIZE,
};