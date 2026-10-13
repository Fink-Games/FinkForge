using System;
using NUnit.Framework;
using Cysharp.Threading.Tasks;
using FinkForge.Config;
using FinkForge.Core;
using FinkForge.Game;
using FinkForge.Players;
using FinkForge.Transport;
using UnityEngine;

namespace FinkForge.Tests
{
    public sealed class GameServiceTests
    {
        private FinkRuntimeState state;
        private PlayerService players;
        private ConfigService config;
        private RecordingTransport transport;
        private FinishDeliveryOutbox outbox;
        private GameService game;
        private FinkForgeConfig configAsset;
        private FinkForgeSettingsService settings;

        [SetUp]
        public void SetUp()
        {
            configAsset = ScriptableObject.CreateInstance<FinkForgeConfig>();
            typeof(FinkForgeConfig).GetField("gameName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(configAsset, "test-game");
            state = new FinkRuntimeState { Initialization = FinkInitializationState.Ready };
            config = new ConfigService();
            config.ApplyRemoteConfig(2, "tr", 1f, 1f, true, 30f, 20f, true);
            players = new PlayerService(() => state.CurrentSession?.State ?? FinkSessionState.None,
                () => config.ScoreEnabled);
            transport = new RecordingTransport();
            outbox = new FinishDeliveryOutbox(transport);
            var heartbeat = new HeartbeatService(transport);
            settings = new FinkForgeSettingsService(configAsset);
            game = new GameService(players, config, outbox, heartbeat, state, settings);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(configAsset);
        }

        [Test]
        public void StartWhileInitializingIsDeferred()
        {
            state.Initialization = FinkInitializationState.Initializing;
            game.Start();
            Assert.IsTrue(state.PendingStart);
            Assert.IsNull(state.CurrentSession);
        }

        [Test]
        public void ReadyFlushesPendingStartAndSendsContractPayload()
        {
            state.Initialization = FinkInitializationState.Initializing;
            game.Start();
            state.Initialization = FinkInitializationState.Ready;
            game.FlushPendingStart();

            Assert.AreEqual(FinkSessionState.Active, state.CurrentSession.State);
            Assert.AreEqual(FinkDeliveryType.AppStart, transport.LastDelivery.Type);
            StringAssert.Contains("game_started", transport.LastDelivery.PayloadJson);
        }

        [Test]
        public void SessionStartedIsRaisedAfterStartDeliveryIsAccepted()
        {
            var started = false;
            game.SessionStarted += () => started = true;

            game.Start();

            Assert.IsTrue(started);
        }

        [Test]
        public void ScoreUsesSeatIdAndIsNotSentWhenUntouched()
        {
            game.Start();
            game.End();
            Assert.AreEqual(FinkDeliveryType.AppStart, transport.LastDelivery.Type);
            Assert.IsFalse(transport.SawScoreDelivery);
            Assert.AreEqual(FinkSessionState.Finished, state.CurrentSession.State);
        }

        [Test]
        public void EndSendsImmutableScorePayloadWithoutAppEnd()
        {
            players.ReplacePlayers(new[] { new FinkPlayer(0, "Player 1") });
            game.Start();
            players.SetScore(0, 0);
            game.End();

            Assert.IsTrue(transport.SawScoreDelivery);
            StringAssert.Contains("\"seatId\":0", transport.ScorePayload);
            StringAssert.Contains("\"score\":0", transport.ScorePayload);
            Assert.AreEqual(FinkDeliveryType.IndividualScores, transport.LastDelivery.Type);
            Assert.AreEqual(FinkSessionState.Finished, state.CurrentSession.State);
        }

        [Test]
        public void SettingsUseAssetGameNameWhenNoRuntimeOverrideExists()
        {
            Assert.AreEqual("test-game", settings.GameName);
        }

        [Test]
        public void ConfigureGameTrimsAndOverridesAssetGameName()
        {
            Assert.IsTrue(settings.ConfigureGame("  SumoFight  "));
            Assert.AreEqual("SumoFight", settings.GameName);
        }

        [Test]
        public void ConfigureGameIgnoresWhitespaceAndKeepsAssetGameName()
        {
            Assert.IsFalse(settings.ConfigureGame("  \t"));
            Assert.AreEqual("test-game", settings.GameName);
        }

        [Test]
        public void ConfigureGameBeforeStartIsUsedByScorePayload()
        {
            settings.ConfigureGame("SumoFight");
            players.ReplacePlayers(new[] { new FinkPlayer(0, "Player 1") });
            game.Start();
            players.SetScore(0, 100);
            game.End();

            StringAssert.Contains("\"gameName\":\"SumoFight\"", transport.ScorePayload);
        }

        [Test]
        public void ConfigureGameDuringActiveSessionDoesNotChangeItsScorePayload()
        {
            players.ReplacePlayers(new[] { new FinkPlayer(0, "Player 1") });
            game.Start();
            settings.ConfigureGame("NextGame");
            players.SetScore(0, 100);
            game.End();

            StringAssert.Contains("\"gameName\":\"test-game\"", transport.ScorePayload);
            StringAssert.DoesNotContain("NextGame", transport.ScorePayload);
        }

        [Test]
        public void RestartAfterFinishedSessionUsesNewGameNameForScores()
        {
            players.ReplacePlayers(new[] { new FinkPlayer(0, "Player 1") });
            game.Start();
            players.SetScore(0, 100);
            game.End();

            settings.ConfigureGame("NextGame");
            game.Restart();
            players.SetScore(0, 200);
            game.End();

            StringAssert.Contains("\"gameName\":\"NextGame\"", transport.ScorePayload);
        }

        [Test]
        public void DuplicateEndDoesNotCreateDuplicateDeliveries()
        {
            game.Start();
            game.End();
            var count = transport.SendCount;
            game.End();
            Assert.AreEqual(count, transport.SendCount);
        }

        [Test]
        public void RestartTwiceInOneFrameCreatesOneNewSession()
        {
            game.Start();
            game.Restart();
            var id = state.CurrentSession.Id;
            game.Restart();
            Assert.AreEqual(id, state.CurrentSession.Id);
        }

        [Test]
        public void RestartUsesSingleAuthoritativeRestartDelivery()
        {
            game.Start();
            var before = transport.SendCount;
            game.Restart();

            Assert.AreEqual(FinkDeliveryType.AppRestart, transport.LastDelivery.Type);
            Assert.AreEqual(before + 1, transport.SendCount);
            StringAssert.Contains("game_restart", transport.LastDelivery.PayloadJson);
        }

        [Test]
        public void RestartDoesNotSendScoreDelivery()
        {
            players.ReplacePlayers(new[] { new FinkPlayer(0, "Player 1") });
            game.Start();
            players.SetScore(0, 100);

            game.Restart();

            Assert.IsFalse(transport.SawScoreDelivery);
            Assert.AreEqual(FinkDeliveryType.AppRestart, transport.LastDelivery.Type);
        }

        [Test]
        public void AddScoreBuildsOnTheExistingSeatScore()
        {
            players.ReplacePlayers(new[] { new FinkPlayer(0, "Player 1") });
            game.Start();
            players.SetScore(0, 10);
            players.AddScore(0, 5);

            Assert.IsTrue(players.TryGetScore(0, out var score));
            Assert.AreEqual(15, score);
        }

        [Test]
        public void ScoreDisabledIgnoresScoreCallsAndFinishesWithoutScoreDelivery()
        {
            config.ApplyRemoteConfig(2, "tr", 1f, 1f, true, 30f, 20f, false);
            players.ReplacePlayers(new[] { new FinkPlayer(0, "Player 1") });
            game.Start();
            players.SetScore(0, 100);
            game.End();

            Assert.IsFalse(transport.SawScoreDelivery);
            Assert.AreEqual(FinkSessionState.Finished, state.CurrentSession.State);
        }

        [Test]
        public void RestartAfterScoresStartsNewSessionWithoutSendingScoresAgain()
        {
            players.ReplacePlayers(new[] { new FinkPlayer(0, "Player 1") });
            game.Start();
            players.SetScore(0, 100);

            game.End();
            Assert.IsTrue(transport.SawScoreDelivery);

            game.Restart();

            Assert.AreEqual(FinkDeliveryType.AppStart, transport.LastDelivery.Type);
            Assert.AreEqual(FinkSessionState.Active, state.CurrentSession.State);
        }

        [Test]
        public void QuitFlushesScoreBeforeAppEnd()
        {
            players.ReplacePlayers(new[] { new FinkPlayer(0, "Player 1") });
            game.Start();
            players.SetScore(0, 0);
            game.Quit();

            Assert.IsTrue(transport.SawScoreDelivery);
            Assert.AreEqual(FinkDeliveryType.AppEnd, transport.LastDelivery.Type);
        }

        [Test]
        public void EndThenQuitSendsAppEndAfterScores()
        {
            players.ReplacePlayers(new[] { new FinkPlayer(0, "Player 1") });
            game.Start();
            players.SetScore(0, 42);

            game.End();
            game.Quit();

            Assert.IsTrue(transport.SawScoreDelivery);
            Assert.AreEqual(FinkDeliveryType.AppEnd, transport.LastDelivery.Type);
        }

        [Test]
        public void TeamScoresUseTeamEndpointWhenPlayersHaveTeamIds()
        {
            players.ReplacePlayers(new[]
            {
                new FinkPlayer(0, "TakımA", 0),
                new FinkPlayer(1, "TakımA", 0),
                new FinkPlayer(2, "TakımB", 1),
                new FinkPlayer(3, "TakımB", 1)
            });
            game.Start();
            players.SetScore(0, 100);
            players.AddScore(1, 50);
            players.SetScore(2, 20);
            players.AddScore(3, 30);

            game.End();

            Assert.AreEqual(FinkDeliveryType.TeamScores, transport.LastDelivery.Type);
            StringAssert.Contains("\"teamId\":1", transport.LastDelivery.PayloadJson);
            StringAssert.Contains("\"score\":150", transport.LastDelivery.PayloadJson);
        }

        private sealed class RecordingTransport : IFinkTransport
        {
            public int SendCount { get; private set; }
            public PendingDelivery LastDelivery { get; private set; }
            public bool SawScoreDelivery { get; private set; }
            public string ScorePayload { get; private set; }

            public void Prepare() { }
            public UniTask LoadBootstrapDataAsync(PlayerService players, ConfigService config) => UniTask.CompletedTask;

            public async UniTask<bool> SendAsync(PendingDelivery delivery)
            {
                await UniTask.CompletedTask;
                if (delivery.Type != FinkDeliveryType.Heartbeat)
                    SendCount++;
                if (delivery.Type != FinkDeliveryType.Heartbeat)
                    LastDelivery = delivery;
                if (delivery.Type == FinkDeliveryType.IndividualScores || delivery.Type == FinkDeliveryType.TeamScores)
                {
                    SawScoreDelivery = true;
                    ScorePayload = delivery.PayloadJson;
                }
                return true;
            }
        }
    }
}
