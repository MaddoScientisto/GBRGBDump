import "./style.css";
import {
  PicNRecWebSerialClient,
  createSavImageBuffer,
  formatHexDump,
  protocolConstants,
} from "@maddoscientisto/picnrec-web-serial";

const app = document.querySelector<HTMLDivElement>("#app");

if (!app) {
  throw new Error("App root element was not found.");
}

app.innerHTML = `
  <main class="shell">
    <section class="hero">
      <p class="eyebrow">Web Serial Test Harness</p>
      <h1>PicNRec raw image dumper</h1>
      <p class="lead">
        This wrapper app is intentionally private. It exists only to validate the workspace library
        against a real browser Web Serial environment.
      </p>
    </section>

    <section class="panel controls">
      <div class="panel-header">
        <h2>Connection</h2>
        <span id="support-pill" class="pill">Checking browser support</span>
      </div>

      <div class="control-grid">
        <label class="field wide">
          <span>Selected port</span>
          <output id="port-label">No serial port selected</output>
        </label>

        <label class="field checkbox-field">
          <input id="fast-mode" type="checkbox" />
          <span>Attempt fast mode (${protocolConstants.FAST_BAUD_RATE} baud)</span>
        </label>

        <div class="button-row wide">
          <button id="request-port">Choose serial port</button>
          <button id="connect">Connect</button>
          <button id="disconnect" class="secondary">Disconnect</button>
        </div>
      </div>
    </section>

    <section class="panel controls">
      <div class="panel-header">
        <h2>Device actions</h2>
        <span id="status-pill" class="pill muted">Disconnected</span>
      </div>

      <div class="control-grid">
        <label class="field">
          <span>Image number</span>
          <input id="image-number" type="number" min="0" step="1" value="0" />
        </label>

        <label class="field">
          <span>Retries</span>
          <input id="retry-count" type="number" min="1" max="10" step="1" value="3" />
        </label>

        <div class="button-row wide">
          <button id="read-last" class="secondary">Read last image number</button>
          <button id="dump-image">Dump selected image</button>
          <button id="download-bin" class="secondary">Download latest .bin</button>
          <button id="download-sav" class="secondary">Download latest .sav</button>
        </div>
      </div>
    </section>

    <section class="panel output-panel">
      <div class="panel-header">
        <h2>Latest dump</h2>
        <span id="dump-summary" class="pill muted">No data loaded</span>
      </div>
      <pre id="hex-dump" class="hex-dump">Connect to a device and dump an image to inspect its raw bytes.</pre>
    </section>

    <section class="panel log-panel">
      <div class="panel-header">
        <h2>Session log</h2>
        <button id="clear-log" class="ghost">Clear log</button>
      </div>
      <div id="log-output" class="log-output"></div>
    </section>
  </main>
`;

const elements = {
  supportPill: document.querySelector<HTMLSpanElement>("#support-pill"),
  statusPill: document.querySelector<HTMLSpanElement>("#status-pill"),
  portLabel: document.querySelector<HTMLOutputElement>("#port-label"),
  fastMode: document.querySelector<HTMLInputElement>("#fast-mode"),
  imageNumber: document.querySelector<HTMLInputElement>("#image-number"),
  retryCount: document.querySelector<HTMLInputElement>("#retry-count"),
  dumpSummary: document.querySelector<HTMLSpanElement>("#dump-summary"),
  hexDump: document.querySelector<HTMLPreElement>("#hex-dump"),
  logOutput: document.querySelector<HTMLDivElement>("#log-output"),
  requestPort: document.querySelector<HTMLButtonElement>("#request-port"),
  connect: document.querySelector<HTMLButtonElement>("#connect"),
  disconnect: document.querySelector<HTMLButtonElement>("#disconnect"),
  readLast: document.querySelector<HTMLButtonElement>("#read-last"),
  dumpImage: document.querySelector<HTMLButtonElement>("#dump-image"),
  downloadBin: document.querySelector<HTMLButtonElement>("#download-bin"),
  downloadSav: document.querySelector<HTMLButtonElement>("#download-sav"),
  clearLog: document.querySelector<HTMLButtonElement>("#clear-log"),
};

for (const [name, element] of Object.entries(elements)) {
  if (!element) {
    throw new Error(`Missing required UI element: ${name}`);
  }
}

const state = {
  client: new PicNRecWebSerialClient({ logger: logMessage }),
  isBusy: false,
  latestDump: null as { bytes: Uint8Array; filename: string } | null,
};

function logMessage(message: string): void {
  const line = document.createElement("div");
  line.className = "log-line";
  line.textContent = `[${new Date().toLocaleTimeString()}] ${message}`;
  elements.logOutput.prepend(line);
}

function updateControls(): void {
  const supported = PicNRecWebSerialClient.isSupported();
  const connected = state.client.isConnected;
  const portSelected = Boolean((state.client as { port?: SerialPort | null }).port);
  const hasDump = Boolean(state.latestDump);

  elements.supportPill.textContent = supported ? "Web Serial available" : "Web Serial unavailable";
  elements.supportPill.className = supported ? "pill ok" : "pill error";
  elements.statusPill.textContent = connected ? `Connected at ${state.client.currentBaudRate}` : "Disconnected";
  elements.statusPill.className = connected ? "pill ok" : "pill muted";
  elements.portLabel.textContent = portSelected ? state.client.getPortSummary() : "No serial port selected";

  elements.requestPort.disabled = state.isBusy || !supported || connected;
  elements.connect.disabled = state.isBusy || !supported || connected;
  elements.disconnect.disabled = state.isBusy || !connected;
  elements.readLast.disabled = state.isBusy || !connected;
  elements.dumpImage.disabled = state.isBusy || !connected;
  elements.downloadBin.disabled = state.isBusy || !hasDump;
  elements.downloadSav.disabled = state.isBusy || !hasDump;
  elements.fastMode.disabled = state.isBusy || connected;
}

async function runBusyAction(label: string, action: () => Promise<void>): Promise<void> {
  if (state.isBusy) {
    logMessage(`Ignored ${label.toLowerCase()} because another action is already running.`);
    return;
  }

  state.isBusy = true;
  updateControls();

  try {
    await action();
  } finally {
    state.isBusy = false;
    updateControls();
  }
}

function setLatestDump(bytes: Uint8Array, filename: string): void {
  state.latestDump = { bytes, filename };
  elements.hexDump.textContent = formatHexDump(bytes);
  elements.dumpSummary.textContent = `${bytes.length} bytes ready as ${filename}`;
  updateControls();
}

function downloadLatestDump(): void {
  if (!state.latestDump) {
    return;
  }

  const blob = new Blob([state.latestDump.bytes], { type: "application/octet-stream" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = state.latestDump.filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

function downloadLatestSav(): void {
  if (!state.latestDump) {
    return;
  }

  const savBytes = createSavImageBuffer(state.latestDump.bytes);
  const savFilename = state.latestDump.filename.replace(/\.bin$/i, ".sav");
  const blob = new Blob([savBytes], { type: "application/octet-stream" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = savFilename;
  anchor.click();
  URL.revokeObjectURL(url);
}

elements.requestPort.addEventListener("click", async () => {
  await runBusyAction("Port selection", async () => {
    try {
      await state.client.requestPort();
    } catch (error) {
      logMessage(`Port selection failed: ${(error as Error).message}`);
    }
  });
});

elements.connect.addEventListener("click", async () => {
  await runBusyAction("Connect", async () => {
    try {
      await state.client.connect({ fastMode: elements.fastMode.checked });
    } catch (error) {
      logMessage(`Connect failed: ${(error as Error).message}`);
    }
  });
});

elements.disconnect.addEventListener("click", async () => {
  await runBusyAction("Disconnect", async () => {
    try {
      await state.client.disconnect();
    } catch (error) {
      logMessage(`Disconnect failed: ${(error as Error).message}`);
    }
  });
});

elements.readLast.addEventListener("click", async () => {
  await runBusyAction("Read last image number", async () => {
    try {
      const lastImageNumber = await state.client.readLastImageNumber();
      elements.imageNumber.value = String(lastImageNumber);
      logMessage(`Last image number updated to ${lastImageNumber}.`);
    } catch (error) {
      logMessage(`Last image read failed: ${(error as Error).message}`);
    }
  });
});

elements.dumpImage.addEventListener("click", async () => {
  const imageNumber = Number(elements.imageNumber.value);
  const retries = Number(elements.retryCount.value);

  if (!Number.isInteger(imageNumber) || imageNumber < 0) {
    logMessage("Image number must be a non-negative integer.");
    return;
  }

  if (!Number.isInteger(retries) || retries < 1) {
    logMessage("Retry count must be at least 1.");
    return;
  }

  await runBusyAction("Image dump", async () => {
    try {
      const image = await state.client.readImage(imageNumber, {
        retries,
        onAttempt: (attempt) => logMessage(`Reading image ${imageNumber}, attempt ${attempt}.`),
      });

      const filename = `image_${String(imageNumber).padStart(4, "0")}.bin`;
      setLatestDump(image, filename);
      logMessage(`Image ${imageNumber} dumped to memory.`);
    } catch (error) {
      logMessage(`Image dump failed: ${(error as Error).message}`);
    }
  });
});

elements.downloadBin.addEventListener("click", () => {
  downloadLatestDump();
  if (state.latestDump) {
    logMessage(`Downloaded ${state.latestDump.filename}.`);
  }
});

elements.downloadSav.addEventListener("click", () => {
  if (!state.latestDump) {
    return;
  }

  downloadLatestSav();
  logMessage(`Downloaded ${state.latestDump.filename.replace(/\.bin$/i, ".sav")} in the legacy .sav format.`);
});

elements.clearLog.addEventListener("click", () => {
  elements.logOutput.innerHTML = "";
});

updateControls();
logMessage(`Protocol block size: ${protocolConstants.BLOCK_SIZE} bytes.`);
logMessage(`Image/save payload size: ${protocolConstants.IMAGE_SIZE} bytes.`);