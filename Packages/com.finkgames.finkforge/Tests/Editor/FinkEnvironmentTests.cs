using System;
using NUnit.Framework;
using FinkForge.Environment;
using FinkForge.Config;
using UnityEngine;

namespace FinkForge.Tests
{
    public sealed class FinkEnvironmentTests
    {
        private static readonly string[] Keys =
        {
            "FINK_ACCESS_TOKEN",
            "FINK_INSTALLATION_ID",
            "FINK_DEVICE_ID",
            "FINK_LAUNCH_NONCE",
            "FINK_API_KEY",
            "FINK_API_PORT"
        };

        private string[] previousValues;
        private FinkForgeConfig config;

        [SetUp]
        public void SetUp()
        {
            previousValues = new string[Keys.Length];
            for (var index = 0; index < Keys.Length; index++)
            {
                previousValues[index] = System.Environment.GetEnvironmentVariable(Keys[index]);
                System.Environment.SetEnvironmentVariable(Keys[index], "test-" + index);
            }

            System.Environment.SetEnvironmentVariable("FINK_API_PORT", "3786");
            config = ScriptableObject.CreateInstance<FinkForgeConfig>();
            SetConfigField("accessTokenIssuer", "test-issuer");
            SetConfigField("accessTokenAudience", "test-audience");
            SetConfigField("accessTokenPurpose", "test-purpose");
            SetConfigField("accessTokenAlgorithm", "RS256");
            SetConfigField("tokenClockSkewSeconds", 30);
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = 0; index < Keys.Length; index++)
                System.Environment.SetEnvironmentVariable(Keys[index], previousValues[index]);
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ResolveReadsAllElectronLaunchValues()
        {
            var environment = new FinkEnvironment(() => ValidPublicKeyPem, config);
            environment.Resolve();

            Assert.IsTrue(environment.IsConfigured);
            Assert.AreEqual("test-0", environment.AccessToken);
            Assert.AreEqual("test-1", environment.InstallationId);
            Assert.AreEqual("test-2", environment.DeviceId);
            Assert.AreEqual("test-3", environment.LaunchNonce);
            Assert.AreEqual("test-4", environment.ApiKey);
            Assert.AreEqual("3786", environment.ApiPort);
            Assert.AreEqual("test-issuer", environment.AccessTokenIssuer);
            Assert.AreEqual("test-audience", environment.AccessTokenAudience);
            Assert.AreEqual("test-purpose", environment.AccessTokenPurpose);
            Assert.AreEqual("RS256", environment.AccessTokenAlgorithm);
            Assert.AreEqual(30, environment.TokenClockSkewSeconds);
            StringAssert.Contains("BEGIN PUBLIC KEY", environment.AccessTokenPublicKeyPem);
            Assert.AreEqual("http://localhost:3786", environment.BaseUrl);
        }

        [Test]
        public void MissingLaunchValueMakesEnvironmentUnconfigured()
        {
            System.Environment.SetEnvironmentVariable("FINK_ACCESS_TOKEN", null);
            var environment = new FinkEnvironment(() => ValidPublicKeyPem, config);
            environment.Resolve();

            Assert.IsFalse(environment.IsConfigured);
            Assert.IsTrue(environment.TryGetConfigurationError(out var error));
            StringAssert.Contains("FINK_ACCESS_TOKEN", error);
        }

        [Test]
        public void InvalidPortDoesNotUseADevelopmentDefault()
        {
            System.Environment.SetEnvironmentVariable("FINK_API_PORT", "not-a-port");
            var environment = new FinkEnvironment(() => ValidPublicKeyPem, config);
            environment.Resolve();

            Assert.IsFalse(environment.IsConfigured);
            Assert.IsEmpty(environment.BaseUrl);
            Assert.IsTrue(environment.TryGetConfigurationError(out var error));
            StringAssert.Contains("FINK_API_PORT", error);
        }

        [Test]
        public void InvalidPublicKeyMakesEnvironmentUnconfiguredWithoutLoggingTheValue()
        {
            var environment = new FinkEnvironment(() => "not-a-pem", config);
            environment.Resolve();

            Assert.IsFalse(environment.IsConfigured);
            Assert.IsTrue(environment.TryGetConfigurationError(out var error));
            StringAssert.Contains("access-token-public-key.pem", error);
            StringAssert.DoesNotContain("not-a-pem", error);
        }

        [Test]
        public void InvalidClockSkewMakesEnvironmentUnconfigured()
        {
            SetConfigField("tokenClockSkewSeconds", -1);
            var environment = new FinkEnvironment(() => ValidPublicKeyPem, config);
            environment.Resolve();

            Assert.IsFalse(environment.IsConfigured);
            Assert.IsTrue(environment.TryGetConfigurationError(out var error));
            StringAssert.Contains("TokenClockSkewSeconds", error);
        }

        [Test]
        public void MissingProjectPublicKeyMakesEnvironmentUnconfigured()
        {
            var environment = new FinkEnvironment(() => string.Empty, config);
            environment.Resolve();

            Assert.IsFalse(environment.IsConfigured);
            Assert.IsTrue(environment.TryGetConfigurationError(out var error));
            StringAssert.Contains("access-token-public-key.pem", error);
        }

        private const string ValidPublicKeyPem =
            "-----BEGIN PUBLIC KEY-----\n" +
            "cHVibGljLWtleQ==\n" +
            "-----END PUBLIC KEY-----";

        private void SetConfigField(string fieldName, object value)
        {
            typeof(FinkForgeConfig).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(config, value);
        }
    }
}
