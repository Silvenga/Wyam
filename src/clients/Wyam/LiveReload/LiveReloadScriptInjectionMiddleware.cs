using System;
using System.IO;
using System.Threading.Tasks;

using AngleSharp;
using AngleSharp.Parser.Html;

using Microsoft.Owin;

namespace Wyam.LiveReload
{
    internal class LiveReloadScriptInjectionMiddleware : OwinMiddleware
    {
        internal HtmlParser HtmlParser { get; set; } = new HtmlParser();

        public LiveReloadScriptInjectionMiddleware(OwinMiddleware next) : base(next)
        {
        }

        public override async Task Invoke(IOwinContext context)
        {
            var originalBody = context.Response.Body;
            var interceptedBody = new MemoryStream();
            context.Response.Body = interceptedBody;

            await Next.Invoke(context);

            if (IsHtmlDocument(context))
            {
                interceptedBody.Position = 0;
                var document = HtmlParser.Parse(interceptedBody);

                var script = document.CreateElement("script");
                script.SetAttribute("type", "text/javascript");
                script.SetAttribute("src", "livereload.js");
                document.Body.Append(script);

                var newContentBuffer = new MemoryStream();
                var writer = new StreamWriter(newContentBuffer);

                document.ToHtml(writer, new AutoSelectedMarkupFormatter());
                writer.Flush();

                context.Response.ContentLength = newContentBuffer.Length;
                newContentBuffer.Position = 0;
                newContentBuffer.CopyTo(originalBody);

                context.Response.Body = originalBody;
            }
            else
            {
                interceptedBody.Position = 0;
                interceptedBody.CopyTo(originalBody);

                context.Response.Body = originalBody;
            }
        }

        private bool IsHtmlDocument(IOwinContext context)
        {
            const string rfc2854Type = "text/html";
            var contentType = context.Response.ContentType;
            return string.Equals(contentType, rfc2854Type, StringComparison.OrdinalIgnoreCase);
        }
    }
}