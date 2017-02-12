using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

using NUnit.Framework;

using Wyam.LiveReload;

namespace Wyam.Tests.LiveReload
{
    [TestFixture]
    public class LiveReloadHostnameTests : IDisposable
    {
        private readonly LiveReloadServer _reloadServer = new LiveReloadServer();

        [Test]
        public void ServerShouldBindWithoutUrlReservations()
        {
            var port = GetEphemeralPort();
            Assert.DoesNotThrow(() => _reloadServer.StartStandaloneHost(port, true));
        }

        [Test]
        public async Task ServerShouldAcceptRequestsFromLocalhost()
        {
            if (IsWindows7OrLower())
            {
                Assert.Inconclusive();
            }
            var port = GetEphemeralPort();
            _reloadServer.StartStandaloneHost(port);

            var client = new HttpClient
            {
                BaseAddress = new Uri($"http://localhost:{port}/")
            };
            var response = await client.GetAsync("livereload.js");

            Assert.IsTrue(response.IsSuccessStatusCode);
        }

        [Test]
        public async Task ServerShouldAcceptRequestsFrom127001()
        {
            if (IsWindows7OrLower())
            {
                Assert.Inconclusive();
            }
            var port = GetEphemeralPort();
            _reloadServer.StartStandaloneHost(port);

            var client = new HttpClient
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}/")
            };
            var response = await client.GetAsync("livereload.js");

            Assert.IsTrue(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }

        private static int GetEphemeralPort()
        {
            // Based on http://stackoverflow.com/a/150974/2001966

            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static bool IsWindows7OrLower()
        {
            // Based on http://stackoverflow.com/a/2732463/2001966
            OperatingSystem os = Environment.OSVersion;
            return (os.Platform == PlatformID.Win32NT) && (os.Version.Major <= 7);
        }

        public void Dispose()
        {
            _reloadServer?.Dispose();
        }
    }
}