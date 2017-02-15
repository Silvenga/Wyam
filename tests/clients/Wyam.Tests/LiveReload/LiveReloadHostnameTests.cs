﻿using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

using NUnit.Framework;

using Wyam.LiveReload;

namespace Wyam.Tests.LiveReload
{
    [TestFixture(Category = "ExcludeFromAppVeyor")]
    public class LiveReloadHostnameTests : IDisposable
    {
        private readonly LiveReloadServer _reloadServer = new LiveReloadServer();

        [Test]
        public void ServerShouldBindWithoutUrlReservations()
        {
            int port = GetEphemeralPort();
            Assert.DoesNotThrow(() => _reloadServer.StartStandaloneHost(port, true));
        }

        [Test]
        public async Task ServerShouldAcceptRequestsFromLocalhost()
        {
            int port = GetEphemeralPort();
            _reloadServer.StartStandaloneHost(port);

            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri($"http://localhost:{port}/")
            };
            HttpResponseMessage response = await client.GetAsync("livereload.js");

            Assert.IsTrue(response.IsSuccessStatusCode);
        }

        [Test]
        public async Task ServerShouldAcceptRequestsFrom127001()
        {
            int port = GetEphemeralPort();
            _reloadServer.StartStandaloneHost(port);

            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}/")
            };
            HttpResponseMessage response = await client.GetAsync("livereload.js");

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

        public void Dispose()
        {
            _reloadServer?.Dispose();
        }
    }
}