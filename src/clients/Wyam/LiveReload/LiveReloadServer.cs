using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.Owin;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.Hosting.Tracing;
using Microsoft.Owin.StaticFiles;

using Owin;
using Owin.WebSocket.Extensions;

using Wyam.Common.Tracing;
using Wyam.Owin;

namespace Wyam.LiveReload
{
    internal class LiveReloadServer : IDisposable
    {
        private readonly ConcurrentBag<IReloadClient> _clients = new ConcurrentBag<IReloadClient>();
        private IDisposable _server;

        public virtual IEnumerable<IReloadClient> ReloadClients => _clients.ToArray();

        public void StartStandaloneHost(int port = 35729, bool throwExceptions = false)
        {
            try
            {
                StartOptions options = new StartOptions($"http://localhost:{port}");
                if (!IsWindows7OrLower()) // The below only works with Windows 8+.
                {
                    options.Urls.Add($"http://127.0.0.1:{port}"); // This must be 127.0.0.1 due to http.sys hostname verification (it allows port sharing), LiveReload hardcodes it though.
                    options.Urls.Add($"http://[::1]:{port}"); // IPv6 Localhost, because why not? All supported platforms should be IPv6 localhost. I hope...
                }
                options.Settings.Add(typeof(ITraceOutputFactory).FullName, typeof(NullTraceOutputFactory).AssemblyQualifiedName);
                _server = WebApp.Start(options, InjectOwinMiddleware);
            }
            catch (Exception ex)
            {
                Trace.Warning($"Error while running the LiveReload server: {ex.Message}");
                if (throwExceptions)
                {
                    throw;
                }
            }

            Trace.Verbose($"LiveReload server listening on port {port}.");
        }

        public void InjectOwinMiddleware(IAppBuilder app)
        {
            // Host livereload.js
            Assembly liveReloadAssembly = typeof(LiveReloadServer).Assembly;
            string rootNamespace = typeof(LiveReloadServer).Namespace;
            IFileSystem reloadFilesystem = new EmbeddedResourceFileSystem(liveReloadAssembly, $"{rootNamespace}");
            app.UseStaticFiles(new StaticFileOptions
            {
                RequestPath = PathString.Empty,
                FileSystem = reloadFilesystem,
                ServeUnknownFileTypes = true
            });

            // Host ws://
            app.MapFleckRoute<ReloadClient>("/livereload", connection => _clients.Add((ReloadClient) connection));
        }

        public void RebuildCompleted(ICollection<string> filesChanged)
        {
            foreach (IReloadClient client in ReloadClients.Where(x => x.IsConnected))
            {
                foreach (string modifiedFile in filesChanged)
                {
                    client.NotifyOfChanges(modifiedFile);
                }
            }
        }

        private static bool IsWindows7OrLower()
        {
            // Based on http://stackoverflow.com/a/2732463/2001966
            OperatingSystem os = Environment.OSVersion;
            return (os.Platform == PlatformID.Win32NT) && (os.Version.Major <= 7);
        }

        public void Dispose()
        {
            _server?.Dispose();
        }
    }
}