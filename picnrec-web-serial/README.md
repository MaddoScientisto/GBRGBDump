# PicNRec Web Serial Workspace

This folder contains a small npm workspace with two parts:

- `packages/picnrec-web-serial`
  - the publishable TypeScript Web Serial client library
- `apps/test-app`
  - a private Vite browser app used to exercise the library locally

The implementation follows the PicNRec protocol behavior used in this repository.

## Library package

Package name:

- `@maddoscientisto/picnrec-web-serial`

The package exports:

- `PicNRecWebSerialClient`
- `decodeLastImageNumber`
- `formatHexDump`
- `createSavImageBuffer`
- `protocolConstants`

The package is configured for GitHub Packages npm publishing and is the only workspace package that gets published.

## Demo app

The demo app stays depends on the workspace package.

It is only there to validate browser behavior and manually test the serial flow without publishing the wrapper app.

## Commands

```bash
npm install
npm run dev
npm run build:lib
npm run build:demo
```

## Requirements

- Node.js 20 or newer
- A Chromium-based browser with Web Serial enabled
- A secure context such as `localhost`