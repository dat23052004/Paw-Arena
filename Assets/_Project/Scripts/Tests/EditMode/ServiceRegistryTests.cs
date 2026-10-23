using System;
using NUnit.Framework;
using PawArena.Core.Services;

namespace PawArena.Tests.EditMode
{
    /// <summary>
    /// Verifies composition-root service registration rules.
    /// </summary>
    public sealed class ServiceRegistryTests
    {
        [SetUp]
        public void SetUp() => ServiceRegistry.Reset();

        [TearDown]
        public void TearDown() => ServiceRegistry.Reset();

        [Test]
        public void Register_WhenServiceIsNew_CanResolveSameInstance()
        {
            var service = new object();

            ServiceRegistry.Register(service);

            Assert.That(ServiceRegistry.GetRequired<object>(), Is.SameAs(service));
        }

        [Test]
        public void Register_WhenContractAlreadyExists_Throws()
        {
            ServiceRegistry.Register(new object());

            Assert.Throws<InvalidOperationException>(() => ServiceRegistry.Register(new object()));
        }
    }
}
