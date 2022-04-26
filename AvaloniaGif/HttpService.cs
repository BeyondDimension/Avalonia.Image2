using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaGif
{
    public static class HttpService
    {
        private static HttpClient Client { get; } = new HttpClient();

        public static async Task<Stream?> GetImageStreamAsync(string requestUri, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var response = await Client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
                .ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var imageStream = await response.Content.ReadAsByteArrayAsync();
                var ms = new MemoryStream(imageStream);
                return ms;
            }
            return default;
        }
    }
}
