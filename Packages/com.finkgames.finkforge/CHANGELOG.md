# Changelog

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
