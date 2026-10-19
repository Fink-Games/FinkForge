# Fink Forge Unity Entegrasyon Rehberi

Bu doküman, Unity oyun kodunun Fink Forge runtime ile nasıl konuşacağını anlatır.

Amaç, oyun içindeki manager sınıflarının backend detaylarını bilmeden şu işleri doğru yapabilmesidir:

- uygulama lifecycle'ını başlatmak, yeniden başlatmak ve bitirmek,
- context'ten gelen oyuncuları okumak,
- oyuncu skorlarını local olarak güncellemek,
- oyun biterken skorların ve lifecycle bilgisinin gönderilmesini sağlamak,
- Unity kapanışını güvenli biçimde yönetmek.

Fink Forge'un public API'si özellikle küçük tutulmuştur. Oyun kodu HTTP endpoint'leri, API key veya outbox/retry ayrıntılarıyla ilgilenmez.

## Package kurulumu

Unity Package Manager üzerinden `Add package from git URL...` seçeneğini açın ve package URL'sini girin:

```text
https://github.com/Fink-Games/FinkForge.git?path=/Packages/com.finkgames.finkforge#main
```

Bu URL geliştirme ve test içindir. Yayınlanan oyunlarda package sürümüyle eşleşen sabit release tag'i kullanın; bu feature'ın release'i `v0.2.0` olacaktır. Package kurulduktan sonra [Fink Forge uygulamasını ve Unity Editor bağlantısını hazırlama](#0-fink-forge-uygulamasını-ve-editor-bağlantısını-hazırlama) adımlarını tamamlayın.

### Birden fazla oyun/proje kullanımı

Aynı Unity projesinde birden fazla oyun profili kullanıyorsanız, `Fink.App.Start()` çağrısından önce aktif oyunun adını runtime'da belirtin:

```csharp
using FinkForge;

Fink.App.ConfigureGame("SumoFight");
Fink.App.Start();
```

Package şu sırayı kullanır: runtime'da verilen geçerli ad, `Resources/FinkForgeConfig.asset` içindeki `gameName`, boş string. Runtime adı aktif session sırasında değiştirilirse mevcut session'ın score payload'ı değişmez; yeni session veya restart sonrasında yeni ad kullanılır.

## 0. Fink Forge uygulamasını ve Editor bağlantısını hazırlama

Fink Forge package'ını kullanmadan önce Fink Forge uygulamasını [resmi kurulum sayfasından](https://dev.fink.games/docs/fink-forge/install) indirin ve çalıştırın.
Unity CLI'nin sisteminizde kurulu ve `unity` komutunun terminalden erişilebilir olduğundan emin olun.

Unity Editor'de çalışırken backend bağlantısının terminal ortamına aktarılması gerekir:

1. Fink Forge uygulamasını açın.
2. Uygulamadaki `Copy Editor settings` butonuna basın.
3. Açık bir terminale kopyalanan komutu yapıştırıp çalıştırın.
4. Unity Editor'ü aynı terminalden Unity CLI ile açın.

Unity CLI kullanıyorsanız önerilen komut, proje klasörünün bulunduğu dizinden çalıştırmaktır:

```bash
unity ./<Project-path>
```

Bu komut macOS, Windows ve Linux'ta Unity CLI tarafından desteklenir. `./<Project-path>` yerine kendi Unity proje klasörünüzün yolunu yazın.

Unity'yi farklı bir terminalden veya Finder/Unity Hub üzerinden açarsanız export edilen launch environment değerleri Unity process'ine aktarılmaz. Ayarlar bir saat geçerlidir; süre dolduğunda Fink Forge uygulamasından yeniden `Copy Editor settings` alın.

## 1. Public API özeti

```csharp
using FinkForge;
```

| İş | API |
|---|---|
| Uygulama oturumunu başlat | `Fink.App.Start()` |
| Mevcut oturumu yeniden başlat | `Fink.App.Restart()` |
| Skorları gönder | `Fink.App.SendScores()` |
| Oyun bitişini bildir | `Fink.App.End()` |
| Session'ın gerçekten başladığını dinle | `Fink.App.OnSessionStarted` |
| Unity/process kapanışı | `Fink.Application.Quit()` |
| Oyuncuları oku | `Fink.Players.GetPlayers()` |
| Oyunun takımlı olduğunu oku | `Fink.Players.UsesTeams` |
| Oyuncuyu oyundan çıkar | `Fink.Players.RemovePlayer(seatId)` |
| Oyuncu ayrılmasını dinle | `Fink.Players.OnPlayerLeft` |
| Oyuncu skoru yaz | `Fink.Players.SetScore(seatId, score)` |
| Oyuncu skorunu artır | `Fink.Players.AddScore(seatId, amount)` |
| Oyuncu skorunu oku | `Fink.Players.TryGetScore(seatId, out score)` |
| Score cache temizlenmesini dinle | `Fink.Players.OnScoresCleared` |
| Skor gönderiminin tamamlanmasını dinle | `Fink.App.OnScoresSent` |
| Müzik ses seviyesi | `Fink.Config.MusicVolume` |
| SFX ses seviyesi | `Fink.Config.SFXVolume` |
| Dil | `Fink.Config.Language` |
| Launch environment hazır mı | `Fink.Launch.IsConfigured` |
| Access token | `Fink.Launch.AccessToken` |
| Installation ID | `Fink.Launch.InstallationId` |
| Device ID | `Fink.Launch.DeviceId` |
| Launch nonce | `Fink.Launch.LaunchNonce` |
| API key | `Fink.Launch.ApiKey` |
| API port | `Fink.Launch.ApiPort` |
| Public verification key (base64) | `Fink.Launch.AccessTokenPublicKeyB64` |
| Token issuer | `Fink.Launch.AccessTokenIssuer` |
| Token audience | `Fink.Launch.AccessTokenAudience` |
| Token purpose | `Fink.Launch.AccessTokenPurpose` |
| Token algorithm | `Fink.Launch.AccessTokenAlgorithm` |
| Token clock skew (seconds) | `Fink.Launch.TokenClockSkewSeconds` |

`Fink.App.End()` skor gönderimini başlatan oyun bitişi çağrısıdır. Gönderim tamamlandığında `Fink.App.OnScoresSent` yayınlanır. Unity/process kapanışı için `Fink.Application.Quit()` kullanılır.

Skor akışı `/v1/context` içindeki `scoreEnabled` alanından gelir. Takım bilgisi oyuncu kayıtlarındaki `teamId` alanlarından türetilir.

## 2. Kurulum ve çalışma modeli

Fink Forge runtime, Unity'nin `BeforeSceneLoad` aşamasında otomatik olarak başlar. Oyunun ayrıca bir bootstrap GameObject'i oluşturmasına gerek yoktur.

Runtime şu sırayı izler:

```text
Unity BeforeSceneLoad
        ↓
Fink Forge runtime bootstrap
        ↓
GET /v1/context
        ↓
Config ve optional player roster yüklenir
        ↓
Fink.App.Start() kullanılabilir
```

`Fink.App.Start()` initialization devam ederken çağrılırsa çağrı kaybedilmez; hazır olunca bir kez çalıştırılmak üzere bekletilir. Initialization başarısız olursa bekleyen start kontrollü biçimde iptal edilir.

Session backend tarafından aktif edildiğinde `Fink.App.OnSessionStarted` bir kez yayınlanır. Bu event hem normal `Start()` hem de başarılı `Restart()` sonrasında kullanılır. `OnPlayersReady`, oyuncu roster'ının hazır olduğunu; `OnSessionStarted`, oyunun oynanabilir session'a geçtiğini belirtir.

Fink Forge Electron uygulaması launch environment değerlerini process environment üzerinden export eder. Unity Editor veya Player build bu değerlerle başlatılmalıdır:

```text
FINK_ACCESS_TOKEN=<token>
FINK_INSTALLATION_ID=<installation-id>
FINK_DEVICE_ID=<device-id>
FINK_LAUNCH_NONCE=<launch-nonce>
FINK_API_KEY=<secret>
FINK_API_PORT=<port>
```

Altı launch değeri Electron environment'ından gelir. Issuer, audience, purpose, algorithm ve clock skew `FinkForgeConfig` ScriptableObject'inden düzenlenir. Developer gerçek `Assets/FinkForge/Resources/access-token-public-key.pem` dosyasını ekler. Package içindeki Editor importer bu dosyayı Unity runtime'da okunabilir `TextAsset` olarak import eder; dosyanın adı ve içeriği PEM olarak kalır. Package mock oyuncu, sahte context veya default port üretmez. Bir değer eksik, public key bulunamaz ya da clock skew geçersizse runtime initialization başarısız olur ve context yüklenmez.

Launch değerlerini diagnostic amaçlı okuyabilirsiniz:

```csharp
bool ready = Fink.Launch.IsConfigured;
string deviceId = Fink.Launch.DeviceId;
string port = Fink.Launch.ApiPort;
```

Access token, API key, installation ID, device ID ve launch nonce Unity asset'lerine serialize edilmez. Public key zaten secret değildir ve developer projesindeki `public.pem` dosyasından okunur; diagnostic loglarda secret veya kişisel launch değerleri yazdırılmaz.

### Access token doğrulama

FinkForge JWT access token'ı context çağrısından önce runtime'da doğrular: RS256 imzası, `exp`, `nbf`, issuer, audience, purpose, installation, device ve nonce claim'leri kontrol edilir. Local server tarafındaki doğrulama yine korunur. Private signing key Unity package'ına veya build'ine kesinlikle eklenmez.

### Electron ile dış geliştirici akışı

1. Fink Forge Electron uygulamasını açın.
2. Context ve oyuncu bilgilerini oluşturun.
3. Launch environment değerlerini export edin.
4. Unity CLI'yi aynı environment ile başlatın: `unity ./<Project-path>`.
5. Oyunu çalıştırın; `/v1/context` authoritative oyuncu ve config kaynağıdır.
6. `Fink.Players.OnPlayersReady` sonrasında oyuncu context'ini test edin.
7. Build alın.
8. Build'i ana Electron sistemine yükleyin; package ek entegrasyon kodu gerektirmeden aynı lifecycle ve header sözleşmesiyle çalışır.

### Context score kontratı

`GET /v1/context` response'u score modelini açıkça belirtmelidir:

```json
{
  "scoreEnabled": true,
  "players": [{ "seatId": 0, "nickname": "Ali" }]
}
```

Bireysel veya skorsuz oyunda oyuncu kayıtlarında `teamId` bulunmaz:

```json
{
  "scoreEnabled": false,
  "players": [{ "seatId": 0, "nickname": "Ali" }]
}
```

Skorsuz ama takımlı oyun da mümkündür:

```json
{
  "scoreEnabled": false,
  "players": [
    { "seatId": 0, "nickname": "TakımA", "teamId": 0 },
    { "seatId": 1, "nickname": "TakımA", "teamId": 0 },
    { "seatId": 2, "nickname": "TakımB", "teamId": 1 },
    { "seatId": 3, "nickname": "TakımB", "teamId": 1 }
  ]
}
```

Bu durumda `usesTeams` lifecycle payload'ında `true` olur; ancak hiçbir score endpoint'i çağrılmaz.

`scoreEnabled: false` ise skor işlemleri tamamen pasiftir. `scoreEnabled: true` ise skor gönderimi açılır. Herhangi bir oyuncuda `teamId` varsa oyun takımlı kabul edilir. Developer her iki modda da yalnızca `SetScore(seatId, score)` veya `AddScore(seatId, amount)` kullanır.

Takım bilgisini `OnPlayersReady` sonrasında okuyabilirsiniz:

```csharp
bool usesTeams = Fink.Players.UsesTeams;
```

Bu değer `scoreEnabled` değerinden bağımsızdır. Skorsuz bir oyun takımlı olabilir.

### Inactivity uyarısı ve timeout event'i

Context içindeki `inactivityTimeoutSeconds` kadar süre input alınmazsa inactivity uyarısı başlar. Bu uyarı sırasında `inactivityQuitCountdownSeconds` kadar süre boyunca input gelmezse `OnTimeout` event'i yayınlanır. Fink Forge kendi başına uygulamayı kapatmaz; oyun bu event'te kendi kararını verir. Oyuncu ekrana dokunur veya input üretirse countdown iptal edilir.

Oyun kendi UI'ında oyuncuya uyarı gösterebilir:

```csharp
Fink.Inactivity.OnWarningStarted += ShowInactivityWarning;
Fink.Inactivity.OnWarningUpdated += UpdateInactivityCountdown;
Fink.Inactivity.OnWarningCancelled += HideInactivityWarning;
Fink.Inactivity.OnTimeout += HandleInactivityTimeout;

void ShowInactivityWarning(float seconds) { /* "Ekrana dokun" UI'ını aç */ }
void UpdateInactivityCountdown(float seconds) { /* countdown text'ini güncelle */ }
void HideInactivityWarning() { /* UI'ı kapat */ }
void HandleInactivityTimeout()
{
    // Oyun kendi kararını verir; gerekiyorsa burada Fink.Application.Quit() çağırılır.
}
```

`Fink.Inactivity.IsWarningActive` ve `Fink.Inactivity.WarningRemainingSeconds` değerleri de UI tarafından okunabilir.

### Masa pause durumu

Aktif session sırasında package, kısa aralıklarla `GET /v1/game/pause` endpoint'ini kontrol eder. Kredi ekranı Electron tarafından gösterilir; Unity oyunu bu ekranı oluşturmaz. Response içindeki `paused: true` değeri geldiğinde `Fink.Application.OnPauseStateChanged` yayınlanır ve inactivity timer durur. Oyun bu event'te gameplay akışını pause eder. `paused: false` geldiğinde event tekrar yayınlanır ve oyun ile inactivity devam eder.

```csharp
Fink.Application.OnPauseStateChanged += HandlePauseState;

void HandlePauseStateChanged(bool paused, string reason, string until)
{
    if (paused)
        PauseGameplay(reason, until);
    else
        ResumeGameplay();
}
```

Örnek response:

```json
{
  "success": true,
  "paused": true,
  "reason": "credit_exhausted",
  "until": "2026-10-16T18:04:30.000Z"
}
```

### Oyun ayarını oluşturma

Score delivery için oyun adını `FinkForgeConfig` üzerinden tanımlayın:

1. Unity menüsünden `Tools > Fink Forge > Create Config` seçin.
2. Oluşturulan `Assets/FinkForge/Resources/FinkForgeConfig.asset` asset'ini açın.
3. `Game Name` alanını doldurun.
4. `Tools > Fink Forge > Validate Project` ile `Config` ve `GameName` değerlerinin `OK` olduğunu kontrol edin.

Config asset'i `Resources/FinkForgeConfig.asset` altında tutulur ve runtime tarafından otomatik okunur.

## 3. Backend lifecycle eşleşmesi

| Unity kodu | HTTP isteği | Payload status |
|---|---|---|
| `Fink.App.Start()` | `POST /v1/app/start` | `game_started` |
| `Fink.App.Restart()` | `POST /v1/app/restart` | `game_restart` |
| Aktif session heartbeat | `POST /v1/health/heartbeat` | body yok |
| `Fink.App.SendScores()` | `POST /v1/game/scores` veya `POST /v1/game/team-scores` | score snapshot |
| `Fink.App.End()` | `Fink.App.SendScores()` çağrısı | score delivery |
| `Fink.Application.Quit()` | `POST /v1/app/end` | `game_quit` |

Lifecycle payload'larını package oluşturur; Game Manager yalnızca ilgili public lifecycle metodunu çağırır.

### Start

```csharp
Fink.App.Start();
```

Başarılı start sonrasında local session aktif olur. Aynı session aktifken tekrar `Start()` çağrısı duplicate start üretmez.

Backend `403` dönerse masa/session aktif kabul edilmez.

### Restart

```csharp
Fink.App.Restart();
```

Restart tek bir authoritative request olarak çalışır:

```text
POST /v1/app/restart
```

Başarılı restart sonrasında mevcut score cache temizlenir ve bir sonraki local session oluşturulur. Aynı frame içinde duplicate restart çağrısı engellenir.

Aktif oyun sırasında `Restart()` score delivery başlatmaz; `/v1/app/restart` gönderir. `End()` veya `SendScores()` sonrasında session artık aktif değildir. Bu durumda aynı `Restart()` çağrısı score göndermeden `/v1/app/start` ile yeni session başlatır.

Score cache temizlendiğinde `Fink.Players.OnScoresCleared` event'i yayınlanır. PlayerManager bu event ile score panellerini sıfırlar.

### End ve score gönderimi

```csharp
Fink.App.End();
```

`End()` doğrudan `SendScores()` çağırır. Package immutable score snapshot'ı hazırlar:

```text
Individual score varsa:
    POST /v1/game/scores

Team score varsa:
    POST /v1/game/team-scores

Score yoksa:
    score isteği gönderilmez
```

Aynı session için ikinci `End()` veya `SendScores()` duplicate score delivery oluşturmaz. Gönderim tamamlandığında `OnScoresSent(bool success)` yayınlanır.

Final ekranda yeniden başlat seçeneği:

```csharp
public void RestartAfterGame()
{
    Fink.App.Restart();
}
```

Bu çağrı score gönderimi tamamlandıktan sonra yapılırsa `/v1/app/start` ile yeni session başlatır.

Final ekranda oyundan çıkış seçeneği:

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

Final ekranda önce `End()` çağrılır. `OnScoresSent(true)` geldiğinde `Restart()` veya `Fink.Application.Quit()` seçeneğini açın. `Quit()` `/v1/app/end` gönderir ve Unity'yi kapatır.

## 4. GameManager nasıl yazılır?

Game Manager'ın sorumluluğu oyun kurallarını yönetmek ve lifecycle kararlarını Fink Forge'a bildirmektir. HTTP, retry, API key veya JSON bilmemelidir.

```csharp
using FinkForge;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    [SerializeField] private bool startAutomatically = true;

    private void Start()
    {
        if (startAutomatically)
            StartGame();
    }

    public void StartGame()
    {
        Fink.App.Start();
    }

    public void RestartGame()
    {
        Fink.App.Restart();
    }

    public void RestartAfterGame()
    {
        Fink.App.Restart();
    }

    public void EndGame()
    {
        Fink.App.End();
    }

}
```

### GameManager için önerilen kurallar

1. `Fink.App.Start()` çağrısını oyun gerçekten oynanabilir hale geldiğinde yap.
2. `Fink.App.Restart()` çağrısını bir sonraki oyun turu başlatılırken kullan.
3. Normal oyun bitişinde `Fink.App.End()` çağır.
4. Menüye dönme veya process kapatma sırasında `Fink.Application.Quit()` kullan.
5. Oyun bitip Unity process'i kapanacaksa `Fink.App.End()` ardından `Fink.Application.Quit()` çağır.
6. Score göndermek için ayrı bir HTTP çağrısı veya coroutine yazma.

## 5. PlayerManager nasıl yazılır?

Player Manager, Fink Forge'un context'ten sağladığı oyuncu listesini okur ve oyun içindeki görüntü/model katmanına aktarır.

`GetPlayers()` read-only bir liste döndürür. Oyuncu listesi `RemovePlayer(seatId)` ve context akışı üzerinden yönetilir.

Runtime context yüklenmeden `GetPlayers()` boş liste döndürebilir. Roster hazır olduğunda `Fink.Players.OnPlayersReady` event'i bir kez yayınlanır; PlayerManager bu event içinde `RefreshPlayers()` çağırmalıdır. Roster'ın boş olması da geçerli bir hazır durumudur.

Oyuncu ayrılması Fink API'den gelen bir komut değildir. Oyuncunun kendi panelindeki ayrılma/çıkış butonu oyunun UI event'ini üretir. Oyun kodu bu event'i `Fink.Players.RemovePlayer(seatId)` ile bildirir. `OnPlayerLeft` ise bu işlemden sonra UI, oyun state'i veya spawn sisteminin tepki vermesi için kullanılan sonuç event'idir.

Context'te dört oyuncu varsa ve dört oyuncunun tamamı ayrılırsa player roster boşalır. Son oyuncunun `RemovePlayer` çağrısından sonra package otomatik olarak normal quit akışını başlatır. Bu akış score delivery ve `app/end` sırasını korur; oyun kodunun ayrıca `Fink.Application.Quit()` çağırmasına gerek yoktur.

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

    public void RefreshPlayers()
    {
        playersBySeat.Clear();

        IReadOnlyList<FinkPlayer> players = Fink.Players.GetPlayers();
        foreach (FinkPlayer player in players)
            playersBySeat[player.SeatId] = player;

        foreach (PlayerPanel panel in panels)
            RefreshPanel(panel.SeatId);
    }

    public bool TryGetPlayer(int seatId, out FinkPlayer player)
    {
        return playersBySeat.TryGetValue(seatId, out player);
    }

    // Oyuncu panelindeki "Ayrıl" butonunun callback'i buraya bağlanır.
    public void HandleLeaveButton(int seatId)
    {
        Fink.Players.RemovePlayer(seatId);
    }

    private void HandlePlayerLeft(FinkPlayer player)
    {
        playersBySeat.Remove(player.SeatId);
        RefreshPanel(player.SeatId);
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

`FinkPlayer` alanları:

```csharp
FinkPlayer.SeatId     // Oyuncuyu tanımlayan koltuk/sıra numarası.
FinkPlayer.Nickname   // Bireysel oyunda oyuncu adı; takım oyununda takım etiketi.
FinkPlayer.TeamId     // Takım yoksa null.
```

`SeatId` yalnızca mevcut oyun oturumundaki oyuncu anahtarıdır. Kalıcı kullanıcı kimliği, save key veya analytics identity olarak kullanılmamalıdır.

### Basit oyuncu paneli

Panel yalnızca görüntüleme sorumluluğuna sahiptir. Roster ve score dağıtımını `PlayerManager` yapar; panel Fink API'sine doğrudan bağlanmaz:

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

Bu script'i dört panelde kullanıp `seatId` değerlerini `0`, `1`, `2` ve `3` yapın.

### Oyuncu oturma düzeni

Oyunun dört oyunculu UI yerleşiminde `seatId` değerleri şu sabit bölgelere karşılık gelir:

```text
+-------------------------------------------------------------+
|                                                             |
|                           [2]                               |
|                                                             |
|      [3]                                       [1]          |
|                                                             |
|                           [0]                               |
|                                                             |
+-------------------------------------------------------------+
```

| `seatId` | UI bölgesi |
|---:|---|
| `0` | Alt |
| `1` | Sağ |
| `2` | Üst |
| `3` | Sol |

UI tasarımı `1900 × 1900` referans çözünürlük üzerinden yapılır. Canvas için `Reference Resolution` değerini `1900 × 1900` kullanın ve oyuncu panellerini bu dört bölgeye anchor'layın. Runtime'da gerçek ekran farklı olabilir; layout scale ve anchor sistemiyle adapte olmalıdır.

`seatId` oyuncu panelinin konumunu belirler; oyuncu listesindeki sıra veya local spawn sırası kullanılmaz. Roster eksikse ilgili bölge boş bırakılır.

## 6. Oyuncu skoru yazmak

Score API yalnızca local session state'i günceller. HTTP delivery End aşamasında package tarafından yapılır.

```csharp
using FinkForge;

public void SetPlayerScore(int seatId, int score)
{
    Fink.Players.SetScore(seatId, score);
}
```

`0` geçerli bir skordur:

```csharp
Fink.Players.SetScore(seatId, 0);
```

Score değiştikçe tekrar tekrar `SetScore` çağrılabilir. Son snapshot End sırasında kullanılır.

Score değişimini dinlemek için Fink Forge public API'sindeki `OnScoreChanged` event'ine subscribe olunabilir. Event, `SetScore` çağrısından sonra `seatId` ve güncel score ile yayınlanır:

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

### Oyuncu skorunu okumak

Score daha önce yazıldıysa `TryGetScore` ile okunabilir. Score henüz set edilmediyse `false` döner:

```csharp
if (Fink.Players.TryGetScore(seatId, out int score))
{
    scoreLabel.text = score.ToString();
}
```

### Oyuncu adı kuralı

Individual score endpoint'i `playerName` bekler. Package bu adı context'ten gelen authoritative oyuncu kaydından alır; oyun manager'ı player name üretmez.

## 7. Score modu ve takım oyunu

```csharp
using FinkForge;

public void SetPoints(int seatId, int points)
{
    Fink.Players.AddScore(seatId, points);
}
```

Developer her zaman seat üzerinden aynı API'yi kullanır. Takımlı oyunda package, oyuncu skorlarını `teamId` bilgisine göre gruplayıp `/v1/game/team-scores` endpoint'ine gönderir. Takımsız oyunda `/v1/game/scores` kullanılır.

Mutlak skor yazmak için:

```csharp
Fink.Players.SetScore(seatId, score);
```

Takım bilgisi context'teki oyuncu `teamId` alanından gelir. Developer takım skorunu ayrıca hesaplamaz veya ayrı bir team score API'si çağırmaz.

Takım score payload'ı:

```json
{
  "gameName": "sumo",
  "playerCount": 4,
  "teams": [
    { "teamId": 1, "teamName": "Kırmızı", "score": 200 }
  ]
}
```

## 8. Config, scene ve kapanış

```csharp
float musicVolume = Fink.Config.MusicVolume;
float sfxVolume = Fink.Config.SFXVolume;
string language = Fink.Config.Language;
```

Müzik ve SFX seviyeleri ayrı değerlerdir. Music mixer kanalına `MusicVolume`, SFX mixer kanalına `SFXVolume` uygulanmalıdır. Başka bir scene'e geçmek process'i kapatmıyorsa `Fink.Application.Quit()` çağırma ve aynı session için tekrar `Fink.App.Start()` çağırma.

Aktif session'ı bitirip menüye dönüyorsan:

```csharp
Fink.App.End();
```

Oyun bitince önce skorları gönder:

```csharp
Fink.App.End();
```

`OnScoresSent(true)` sonrasında yeni session için `Fink.App.Restart()`, process kapanışı için `Fink.Application.Quit()` çağır. `Fink.Application.Quit()` `/v1/app/end` gönderir ve Unity kapanışını başlatır.

## 9. Otomatik hata bildirimi

Gameplay koduna açık bir incident API'si yoktur. Runtime, Unity'nin kırmızı `Error`, `Exception` ve `Assert` loglarını otomatik yakalar ve duplicate logları filtreleyerek `/v1/health/incident` endpoint'ine outbox/retry üzerinden gönderir. Incident POST'u başarıyla tamamlandıktan sonra runtime uygulama kapanışını başlatır; Unity Player kapanır, Unity Editor'da Play Mode durur. POST başarısızsa retry mekanizması çalışır ve başarılı teslimat olmadan kapanış tetiklenmez.

Gönderilen payload şu alanları içerir:

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

## 10. Entegrasyon kontrol listesi

- [ ] GameManager `Fink.App.Start()`, `Restart()`, `End()` kullanıyor.
- [ ] Process kapanışında `Fink.Application.Quit()` kullanılıyor.
- [ ] PlayerManager `Fink.Players.GetPlayers()` ile roster okuyor.
- [ ] Paneldeki ayrılma butonu `Fink.Players.RemovePlayer(seatId)` çağırıyor.
- [ ] `OnPlayerLeft` subscribe/unsubscribe dengeli.
- [ ] Score `SetScore` veya `AddScore` ile seat üzerinden yönetiliyor.
- [ ] Score okumak için `TryGetScore` kullanılıyor.
- [ ] Session için seçilen score modeli tutarlı: individual veya team.
- [ ] Runtime bağlantısı package public API'si üzerinden yapılıyor.
- [ ] Oyuncu ve takım adları context verisinden okunuyor.

Bu sınırlar içinde oyun kendi GameManager, PlayerManager, UI ve scoring kurallarını özgürce kurabilir; Fink Forge yalnızca runtime lifecycle, roster, score snapshot ve otomatik hata bildirimini yönetir.
