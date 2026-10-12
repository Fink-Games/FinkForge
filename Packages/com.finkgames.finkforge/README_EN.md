# Fink Forge Unity Integration Guide

This guide explains how Unity game code communicates with the Fink Forge runtime.

The goal is to let game managers handle lifecycle, players, scores, configuration, and shutdown without knowing HTTP endpoints, JSON payloads, API keys, or retry details.

## 0. Install Fink Forge and connect the Unity Editor

Before using the Fink Forge package, download and run the Fink Forge application from the [official installation page](https://dev.fink.games/docs/fink-forge/install).

When working in the Unity Editor, pass the backend connection settings to the Unity process:

1. Open the Fink Forge application.
2. Click `Copy Editor settings`.
3. Paste and run the copied command in a terminal.
4. Launch the Unity Editor from that same terminal using the Unity CLI.

When using the Unity CLI, the recommended command is run from the directory that contains the project:

```bash
unity ./FinkForge
```

This command is supported by the Unity CLI on macOS, Windows, and Linux. Replace `./FinkForge` with your Unity project folder.

If you need to invoke the Unity executable directly, use the platform-specific forms below:

macOS:
```bash
/Applications/Unity/Hub/Editor/<unity-version>/Unity.app/Contents/MacOS/Unity \\
  -projectPath "/path/to/your/unity-project"
```

Windows PowerShell:
```powershell
& "C:\Program Files\Unity\Hub\Editor\<unity-version>\Editor\Unity.exe" `
  -projectPath "C:\path\to\your\unity-project"
```

Linux:
```bash
<unity-install-path>/Editor/Unity \\
  -projectPath "/path/to/your/unity-project"
```

If Unity is opened from another terminal, Finder, or Unity Hub, the copied `FINK_API_PORT` and `FINK_API_KEY` values are not inherited by the Unity process. The settings are valid for one hour; after that, use `Copy Editor settings` again.

## 1. Public API overview

```csharp
using FinkForge;
```

| Operation | API |
|---|---|
| Start the application session | `Fink.App.Start()` |
| Restart the current session | `Fink.App.Restart()` |
| Send scores | `Fink.App.SendScores()` |
| Signal that the game ended | `Fink.App.End()` |
| Listen for the active session | `Fink.App.OnSessionStarted` |
| Shut down Unity/process | `Fink.Application.Quit()` |
| Read players | `Fink.Players.GetPlayers()` |
| Read whether the game uses teams | `Fink.Players.UsesTeams` |
| Remove a player | `Fink.Players.RemovePlayer(seatId)` |
| Listen for a player leaving | `Fink.Players.OnPlayerLeft` |
| Set a player score | `Fink.Players.SetScore(seatId, score)` |
| Add points to a player score | `Fink.Players.AddScore(seatId, amount)` |
| Read a player score | `Fink.Players.TryGetScore(seatId, out score)` |
| Listen for cleared scores | `Fink.Players.OnScoresCleared` |
| Listen for completed score delivery | `Fink.App.OnScoresSent` |
| Listen for score changes | `Fink.Players.OnScoreChanged` |
| Read the music volume | `Fink.Config.MusicVolume` |
| Read the SFX volume | `Fink.Config.SFXVolume` |
| Read the language | `Fink.Config.Language` |

`Fink.App.End()` starts score delivery for the completed game. When delivery finishes, `Fink.App.OnScoresSent` is raised. Use `Fink.Application.Quit()` when Unity itself is closing.

## 2. Setup and runtime model

Fink Forge starts automatically during Unity's `BeforeSceneLoad` phase. A separate bootstrap GameObject is not required.

The runtime follows this sequence:

```text
Unity BeforeSceneLoad
        ↓
Fink Forge runtime bootstrap
        ↓
GET /v1/context
        ↓
Load config and the optional player roster
        ↓
Fink.App.Start() becomes available
```

Runtime environment values are read from the process environment:

```text
FINK_API_PORT=3786
FINK_API_KEY=<secret>
```

The development default port is `3786`. The API key is kept in the process environment and is not written to Unity assets, the public API, logs, or exception messages.

### Context score contract

`GET /v1/context` must explicitly declare the score model:

```json
{
  "scoreEnabled": true,
  "players": [{ "seatId": 0, "nickname": "Ali" }]
}
```

For an individual or scoreless game, player records do not contain `teamId`:

```json
{
  "scoreEnabled": false,
  "players": [{ "seatId": 0, "nickname": "Ali" }]
}
```

A scoreless team game is also valid:

```json
{
  "scoreEnabled": false,
  "players": [
    { "seatId": 0, "nickname": "TeamA", "teamId": 0 },
    { "seatId": 1, "nickname": "TeamA", "teamId": 0 },
    { "seatId": 2, "nickname": "TeamB", "teamId": 1 },
    { "seatId": 3, "nickname": "TeamB", "teamId": 1 }
  ]
}
```

In this case, lifecycle payloads use `usesTeams: true`, but no score endpoint is called.

When `scoreEnabled` is false, score handling is completely inactive. When it is true, score delivery is enabled. If any player has a `teamId`, the game is treated as a team game. Game code uses only `SetScore(seatId, score)` or `AddScore(seatId, amount)` in both modes.

Read team usage after `OnPlayersReady`:

```csharp
bool usesTeams = Fink.Players.UsesTeams;
```

This value is independent of `scoreEnabled`; a scoreless game can still use teams.

### Creating the game configuration

Create the game configuration through the Unity menu:

1. Select `Tools > Fink Forge > Create Config`.
2. Open `Assets/FinkForge/Resources/FinkForgeConfig.asset`.
3. Fill in `Game Name`.
4. Run `Tools > Fink Forge > Validate Project` and confirm that `Config` and `GameName` report `OK`.

The config asset is loaded automatically from `Resources/FinkForgeConfig.asset`. It does not need to be assigned to a scene or manager.

## 3. Backend lifecycle mapping

| Unity code | HTTP request | Status |
|---|---|---|
| `Fink.App.Start()` | `POST /v1/app/start` | `game_started` |
| `Fink.App.Restart()` | `POST /v1/app/restart` | `game_restart` |
| Active-session heartbeat | `POST /v1/health/heartbeat` | no body |
| `Fink.App.SendScores()` | `POST /v1/game/scores` or `POST /v1/game/team-scores` | score snapshot |
| `Fink.App.End()` | calls `Fink.App.SendScores()` | score delivery |
| `Fink.Application.Quit()` | `POST /v1/app/end` | `game_quit` |

The package creates lifecycle payloads. Game managers only call the corresponding public API.

### Start

```csharp
Fink.App.Start();
```

If initialization is still running, the start request is deferred. Once `/v1/context` completes, the pending start is sent once. Calling `Start()` again while a session is active does not create a duplicate start.

`Fink.App.OnSessionStarted` is raised once the backend has accepted `/v1/app/start` or a successful restart has activated the new session. `OnPlayersReady` means that the player roster is ready; `OnSessionStarted` means that gameplay can use the active session.

### Restart

```csharp
Fink.App.Restart();
```

Restart is one authoritative request:

```text
POST /v1/app/restart
```

After a successful restart, the current score cache is cleared and the next local session is created. Duplicate restart calls in the same frame are ignored.

During an active game, `Restart()` does not start score delivery; it sends `/v1/app/restart`. After `End()` or `SendScores()`, the session is no longer active. The same `Restart()` call then starts a new session with `/v1/app/start`, without sending scores again.

When the score cache is cleared, `Fink.Players.OnScoresCleared` is raised. The PlayerManager uses this event to reset score panels.

### End and score delivery

```csharp
Fink.App.End();
```

`End()` directly calls `SendScores()`. The package takes an immutable score snapshot:

```text
Individual score exists:
    POST /v1/game/scores

Team score exists:
    POST /v1/game/team-scores

No score exists:
    no score request
```

A second `End()` or `SendScores()` call for the same session does not create a duplicate score delivery. Completion raises `OnScoresSent(bool success)`.

For a restart from the final screen:

```csharp
public void RestartAfterGame()
{
    Fink.App.Restart();
}
```

Call this after score delivery completes; it starts the next session with `/v1/app/start`.

For quitting from the final screen:

```csharp
public void QuitAfterGame()
{
    Fink.Application.Quit();
}

private void HandleScoresSent(bool success)
{
    if (success) Fink.Application.Quit();
}
```

Call `End()` first. When `OnScoresSent(true)` arrives, enable the final-screen `Restart()` and `Fink.Application.Quit()` choices. `Quit()` sends `/v1/app/end` and closes Unity.

## 4. Writing a GameManager

A GameManager owns game decisions and reports them to Fink Forge. It does not know about endpoints, JSON, retry behavior, or API keys.

```csharp
using FinkForge;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    public void BeginGame() => Fink.App.Start();

    public void RestartGame() => Fink.App.Restart();

    public void RestartAfterGame() => Fink.App.Restart();

    public void EndGame() => Fink.App.End();

}
```

The game can keep an open score callback and forward score changes to the package:

```csharp
public void OnScoreChanged(int seatId, int score)
{
    Fink.Players.SetScore(seatId, score);
}
```

After `Fink.App.End()`, the package sends the final score snapshot once. Wait for `OnScoresSent` before enabling the final-screen restart or quit action.

## 5. Writing a PlayerManager

A PlayerManager maps the authoritative context roster to game objects. The roster is read-only; player removal is reported from the game's own player panel.

```csharp
using System.Collections.Generic;
using FinkForge;
using UnityEngine;

public sealed class PlayerManager : MonoBehaviour
{
    private readonly Dictionary<int, FinkPlayer> playersBySeat =
        new Dictionary<int, FinkPlayer>();
    [SerializeField] private PlayerPanel[] panels;
    private readonly Dictionary<int, PlayerPanel> panelsBySeat =
        new Dictionary<int, PlayerPanel>();

    private void OnEnable()
    {
        foreach (PlayerPanel panel in panels)
            panelsBySeat[panel.SeatId] = panel;
        Fink.Players.OnPlayersReady += HandlePlayersReady;
        Fink.Players.OnPlayerLeft += HandlePlayerLeft;
        Fink.Players.OnScoreChanged += HandleScoreChanged;
        Fink.Players.OnScoresCleared += HandleScoresCleared;
        RefreshPlayers();
    }

    private void OnDisable()
    {
        Fink.Players.OnPlayersReady -= HandlePlayersReady;
        Fink.Players.OnPlayerLeft -= HandlePlayerLeft;
        Fink.Players.OnScoreChanged -= HandleScoreChanged;
        Fink.Players.OnScoresCleared -= HandleScoresCleared;
    }

    private void HandlePlayersReady()
    {
        RefreshPlayers();
    }

    private void RefreshPlayers()
    {
        playersBySeat.Clear();
        foreach (FinkPlayer player in Fink.Players.GetPlayers())
            playersBySeat[player.SeatId] = player;

        foreach (PlayerPanel panel in panels)
            RefreshPanel(panel.SeatId);
    }

    public void OnLeaveButtonPressed(int seatId)
    {
        Fink.Players.RemovePlayer(seatId);
    }

    private void HandlePlayerLeft(FinkPlayer player)
    {
        playersBySeat.Remove(player.SeatId);
        RefreshPanel(player.SeatId);
        // Remove the player's UI, game state, and spawned objects here.
        Debug.Log("Player left seat " + player.SeatId);
    }

    private void HandleScoreChanged(int seatId, int score)
    {
        RefreshPanel(seatId);
    }

    private void HandleScoresCleared()
    {
        foreach (PlayerPanel panel in panels)
            if (panel != null) RefreshPanel(panel.SeatId);
    }

    private void RefreshPanel(int seatId)
    {
        if (!panelsBySeat.TryGetValue(seatId, out PlayerPanel panel)) return;
        if (!playersBySeat.TryGetValue(seatId, out FinkPlayer player))
        {
            panel.Clear();
            return;
        }

        int score = Fink.Players.TryGetScore(seatId, out var currentScore) ? currentScore : 0;
        panel.Show(player, score);
    }
}
```

`GetPlayers()` can return an empty list before the runtime context has loaded. When the roster is ready, `Fink.Players.OnPlayersReady` is raised once; the PlayerManager calls `RefreshPlayers()` from that event. An empty roster is also a valid ready state.

`OnPlayerLeft` is a result event, not the command itself. `RemovePlayer(seatId)` reports the panel action and then publishes the event for the removed player only. When all four players have left and the roster is empty, the package starts the normal quit flow automatically.

### Simple player panel

The panel is a view only. `PlayerManager` distributes roster and score updates; the panel does not subscribe to the Fink Forge API directly:

```csharp
using FinkForge;
using TMPro;
using UnityEngine;

public sealed class PlayerPanel : MonoBehaviour
{
    [SerializeField] private int seatId;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text scoreLabel;

    public int SeatId => seatId;

    public void Show(FinkPlayer player, int score)
    {
        nameLabel.text = player.Nickname;
        scoreLabel.text = score.ToString();
    }

    public void Clear()
    {
        nameLabel.text = "Empty";
        scoreLabel.text = "0";
    }
}
```

Use this script on four panels and set their `seatId` values to `0`, `1`, `2`, and `3`.

### Player seat layout

For the four-player UI, `seatId` maps to these fixed regions:

```text
+-------------------------------------------------------------+
|                                                             |
|                           [2]                               |
|                                                             |
|      [3]                                       [1]           |
|                                                             |
|                           [0]                               |
|                                                             |
+-------------------------------------------------------------+
```

| `seatId` | UI region |
|---:|---|
| `0` | Bottom |
| `1` | Right |
| `2` | Top |
| `3` | Left |

The UI is designed around a `1900 × 1900` reference resolution. Set the Canvas reference resolution to `1900 × 1900` and anchor player panels to these four regions. Different runtime resolutions should be handled through Canvas scaling and anchors.

The `seatId` determines the player panel location. Do not use the roster iteration order or a local spawn order. If a roster entry is missing, leave its region empty.

`FinkPlayer` exposes:

```csharp
FinkPlayer.SeatId   // Player key for the current game session.
FinkPlayer.Nickname // Player name in individual mode; team label in team mode.
FinkPlayer.TeamId   // Null when the player has no team.
```

`SeatId` is a session-local player key, not a persistent user identity, save key, or analytics identity.

## 6. Player scores

The score API updates local session state. The package sends the snapshot during `End()`.

```csharp
public void SetPlayerScore(int seatId, int score)
{
    Fink.Players.SetScore(seatId, score);
}
```

Zero is a valid score:

```csharp
Fink.Players.SetScore(seatId, 0);
```

The same score can be updated repeatedly; the latest value is used for the final snapshot.

### Listening for score changes

`OnScoreChanged` is a Fink Forge public event. It is raised after `SetScore` with the `seatId` and updated score:

```csharp
private void OnEnable()
{
    Fink.Players.OnScoreChanged += HandleScoreChanged;
}

private void OnDisable()
{
    Fink.Players.OnScoreChanged -= HandleScoreChanged;
}

private void HandleScoreChanged(int seatId, int score)
{
    scoreLabel.text = score.ToString();
}
```

### Reading a player score

`TryGetScore` reads the local score state and returns `false` when no score has been set yet:

```csharp
if (Fink.Players.TryGetScore(seatId, out int score))
{
    scoreLabel.text = score.ToString();
}
```

The authoritative player name used by individual score delivery comes from the context roster.

## 7. Score mode and team games

```csharp
public void AddPoints(int seatId, int points)
{
    Fink.Players.AddScore(seatId, points);
}
```

The developer always uses the same seat-based API. In team mode, the package groups player scores by `teamId` and sends team totals to `/v1/game/team-scores`. In individual mode it uses `/v1/game/scores`.

Set an absolute score with:

```csharp
Fink.Players.SetScore(seatId, score);
```

Team membership comes from the `teamId` field in the authoritative player context. Game code does not calculate team scores or call a separate team-score API.

Team score payload:

```json
{
  "gameName": "sumo",
  "playerCount": 4,
  "teams": [
    { "teamId": 1, "score": 200 }
  ]
}
```

## 8. Config, scenes, and shutdown

```csharp
float musicVolume = Fink.Config.MusicVolume;
float sfxVolume = Fink.Config.SFXVolume;
string language = Fink.Config.Language;
```

Music and SFX levels are separate. Apply `MusicVolume` to the music mixer channel and `SFXVolume` to the SFX mixer channel.

When returning to a menu while Unity remains open, end the game session:

```csharp
Fink.App.End();
```

When the game ends, send scores first:

```csharp
Fink.App.End();
```

After `OnScoresSent(true)`, call either `Fink.App.Restart()` for a new session or `Fink.Application.Quit()` to send `/v1/app/end` and close Unity. A scene change inside the same game session does not restart the app lifecycle.

## 9. Automatic incident reporting

There is no gameplay-facing manual incident API. The runtime automatically captures Unity red `Error`, `Exception`, and `Assert` logs, filters duplicate messages, and sends incidents through the outbox/retry flow to `/v1/health/incident`. After the incident POST succeeds, the runtime starts the application shutdown flow: a Unity Player quits, while the Unity Editor stops Play Mode. If delivery fails, retries continue and shutdown is not triggered before a successful delivery.

The payload contains:

```json
{
  "reasonType": "crash",
  "message": "...",
  "details": {
    "stackTrace": "...",
    "scene": "..."
  }
}
```

## 10. Integration checklist

- [ ] GameManager calls `Fink.App.Start()`, `Restart()`, and `End()`.
- [ ] Process shutdown uses `Fink.Application.Quit()`.
- [ ] PlayerManager reads the roster with `Fink.Players.GetPlayers()`.
- [ ] The player panel calls `Fink.Players.RemovePlayer(seatId)`.
- [ ] `OnPlayerLeft` is subscribed and unsubscribed with the manager lifecycle.
- [ ] Scores use `SetScore` or `AddScore` with a seat id.
- [ ] Score UI uses `OnScoreChanged` or `TryGetScore`.
- [ ] The session uses one score model: individual or team.
- [ ] Runtime communication goes through the Fink Forge public API.
- [ ] Player and team names come from context data.

Within these boundaries, each game can build its own GameManager, PlayerManager, UI, and scoring rules while Fink Forge manages runtime lifecycle, roster state, score snapshots, and automatic incident reporting.
