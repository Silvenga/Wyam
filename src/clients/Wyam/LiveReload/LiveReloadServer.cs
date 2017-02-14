using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Builder;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;

using Owin;
using Owin.WebSocket.Extensions;

using Wyam.Common.Tracing;

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
                //StartOptions options = new StartOptions($"http://localhost:{port}");
                //if (!IsWindows7OrLower()) // The below only works with Windows 8+.
                //{
                //    options.Urls.Add($"http://127.0.0.1:{port}");
                //    // This must be 127.0.0.1 due to http.sys hostname verification (it allows port sharing), LiveReload hardcodes it though.
                //    options.Urls.Add($"http://[::1]:{port}"); // IPv6 Localhost, because why not? All supported platforms should be IPv6 localhost. I hope...
                //}
                //options.Settings.Add(typeof(ITraceOutputFactory).FullName, typeof(NullTraceOutputFactory).AssemblyQualifiedName);
                //_server = WebApp.Start(options, AddLiveReloadHostingMiddleware);

                var host = new WebHostBuilder()
                    .UseKestrel()
                    .UseUrls($"http://localhost:{port}")
                    .Configure(builder =>
                    {
                        builder.UseWebSockets();
                        //builder.Use(async (context, next) =>
                        //{
                        //    if (context.WebSockets.IsWebSocketRequest)
                        //    {
                        //        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                             
                               
                        //    }
                        //    else
                        //    {
                        //        await next();
                        //    }
                        //});

                        builder.UseOwin(app => { AddLiveReloadHostingMiddleware(app); });
                        Console.WriteLine();
                    })
                    .Build();

                host.Start();
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

        public void AddLiveReloadInjectionMiddleware(IAppBuilder app)
        {
            // Inject LR script.
            //app.UseLiveReloadScriptInjections();
        }

        public void AddLiveReloadHostingMiddleware(IAppBuilder app)
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

    // http://stackoverflow.com/a/30742029/2001966
    internal static class IApplicationBuilderExtensions
    {
        public static void UseOwin(
            this IApplicationBuilder app,
            Action<IAppBuilder> owinConfiguration)
        {
            app.UseOwin(
                addToPipeline =>
                {
                    addToPipeline(
                        next =>
                        {
                            var builder = new AppBuilder();

                            owinConfiguration(builder);

                            builder.Run(ctx => next(ctx.Environment));

                            Func<IDictionary<string, object>, Task> appFunc =
                                (Func<IDictionary<string, object>, Task>)
                                builder.Build(typeof(Func<IDictionary<string, object>, Task>));

                            return appFunc;
                        });
                });
        }
    }
}