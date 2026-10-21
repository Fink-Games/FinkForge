# Changelog

## 0.9.0

- Added independent table light event and persistent-state APIs.
- Added short, non-blocking light requests with no retry or outbox delivery.
- Added seat events, multi-seat events, turn state, countdown state, and light-state clearing.

## 0.8.0

- Added polling for `GET /v1/game/pause` during active sessions.
- Added `Fink.Application.OnPauseStateChanged` so games can pause and resume gameplay while Electron displays the credit screen.
- Paused inactivity tracking while the table is paused.
- Unity does not create or control the Electron credit screen.

## 0.7.0

- Added the inactivity warning countdown and automatic application shutdown after the configured inactivity periods.
- Added public inactivity warning events and countdown state for game-owned warning UI.
- Kept heartbeat delivery active after `SendScores()` until restart or quit is requested.

## 0.6.1

- Removed the fixed three-second delay from application shutdown. Editor Play Mode and Player builds now quit immediately after the end flow is requested.

## 0.6.0

- Moved JWT issuer, audience, purpose, algorithm, and clock-skew settings into `FinkForgeConfig`.
- Electron environment is now limited to the six launch/session values.

## 0.5.0

- Added runtime JWT decoding and RS256 signature validation before context initialization.
- Added expiration, not-before, issuer, audience, purpose, installation, device, and nonce claim checks.
- The trusted public key is loaded from the developer project at `Assets/FinkForge/Resources/access-token-public-key.pem` through the package's PEM importer.

## 0.4.0

- Added Electron-exported access-token verification metadata to `Fink.Launch`.
- Added base64 public-key validation and PEM conversion, plus integer clock-skew validation.
- Kept token verification independent from FinkAPI and excluded private signing keys from the package.

## 0.3.0

- Added Electron launch environment support for access token, installation ID, device ID, launch nonce, API key, and API port.
- Added the public `Fink.Launch` diagnostics API and required request headers for the local Fink Forge server.
- Initialization now fails clearly when the exported launch environment is incomplete; no mock context or default port is used.
- Added authoritative context loading and Electron developer workflow documentation.

## 0.2.1

- Fixed application shutdown waiting while the game is paused or `Time.timeScale` is zero.
- Quit now waits three real-time seconds before stopping Unity Editor Play Mode or quitting the Player build.

## 0.2.0

- Added runtime game profile configuration with `Fink.App.ConfigureGame(string gameName)`.
