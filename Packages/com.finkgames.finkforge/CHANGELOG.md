# Changelog

## 0.2.1

- Fixed application shutdown waiting while the game is paused or `Time.timeScale` is zero.
- Quit now waits three real-time seconds before stopping Unity Editor Play Mode or quitting the Player build.

## 0.2.0

- Added runtime game profile configuration with `Fink.App.ConfigureGame(string gameName)`.
