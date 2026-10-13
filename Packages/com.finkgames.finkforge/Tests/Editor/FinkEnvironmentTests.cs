using System;
using NUnit.Framework;
using FinkForge.Environment;

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
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = 0; index < Keys.Length; index++)
                System.Environment.SetEnvironmentVariable(Keys[index], previousValues[index]);
        }

        [Test]
        public void ResolveReadsAllElectronLaunchValues()
        {
            var environment = new FinkEnvironment();
            environment.Resolve();

            Assert.IsTrue(environment.IsConfigured);
            Assert.AreEqual("test-0", environment.AccessToken);
            Assert.AreEqual("test-1", environment.InstallationId);
            Assert.AreEqual("test-2", environment.DeviceId);
            Assert.AreEqual("test-3", environment.LaunchNonce);
            Assert.AreEqual("test-4", environment.ApiKey);
            Assert.AreEqual("3786", environment.ApiPort);
            Assert.AreEqual("http://localhost:3786", environment.BaseUrl);
        }

        [Test]
        public void MissingLaunchValueMakesEnvironmentUnconfigured()
        {
            System.Environment.SetEnvironmentVariable("FINK_ACCESS_TOKEN", null);
            var environment = new FinkEnvironment();
            environment.Resolve();

            Assert.IsFalse(environment.IsConfigured);
            Assert.IsTrue(environment.TryGetConfigurationError(out var error));
            StringAssert.Contains("FINK_ACCESS_TOKEN", error);
        }

        [Test]
        public void InvalidPortDoesNotUseADevelopmentDefault()
        {
            System.Environment.SetEnvironmentVariable("FINK_API_PORT", "not-a-port");
            var environment = new FinkEnvironment();
            environment.Resolve();

            Assert.IsFalse(environment.IsConfigured);
            Assert.IsEmpty(environment.BaseUrl);
            Assert.IsTrue(environment.TryGetConfigurationError(out var error));
            StringAssert.Contains("FINK_API_PORT", error);
        }
    }
}
