using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace IRSI.Services.AutoUpdate.Tests
{
    public abstract class MockHandler : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                var content = await request.Content.ReadAsStringAsync(cancellationToken);
                return SendAsync(request.Method, request.RequestUri?.PathAndQuery, request.Headers, content);
            }

            return SendAsync(request.Method, request.RequestUri?.PathAndQuery, request.Headers, string.Empty);
        }

        public abstract HttpResponseMessage SendAsync(HttpMethod method, string url, HttpRequestHeaders headers,
            string content);
    }
}