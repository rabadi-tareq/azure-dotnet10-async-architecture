// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.Search;
using AsyncTickets.Models.Services;
using AsyncTickets.Web.CallCenter.Infrastructure;
using System.Net;
using System.Text.Json;

namespace AsyncTickets.Web.CallCenter.Services.AsyncTicketsApiServices
{
    public class AsyncTicketsApiConcertSearchService : IConcertSearchService
    {
        private readonly HttpClient httpClient;

        public AsyncTicketsApiConcertSearchService(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<SearchResponse<ConcertSearchResult>> SearchAsync(SearchRequest request)
        {
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "api/Search/Concerts");
            httpRequestMessage.Content = JsonContent.Create(request);
            var httpResponseMessage = await this.httpClient.SendAsync(httpRequestMessage);
            var responseMessage = await httpResponseMessage.Content.ReadAsStringAsync();

            if (httpResponseMessage.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(nameof(SearchAsync), new WebException(responseMessage));
            }

            return JsonSerializer.Deserialize<SearchResponse<ConcertSearchResult>>(responseMessage, AsyncTicketsApiConfiguration.GetSerializerOptions())
                ?? new SearchResponse<ConcertSearchResult>(request, Array.Empty<ConcertSearchResult>(), Array.Empty<SearchFacet>());
        }

        public async Task<ICollection<string>> SuggestAsync(string query)
        {
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, $"api/Search/SuggestConcerts?query={query}");
            var httpResponseMessage = await this.httpClient.SendAsync(httpRequestMessage);
            var responseMessage = await httpResponseMessage.Content.ReadAsStringAsync();

            if (httpResponseMessage.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(nameof(SearchAsync), new WebException(responseMessage));
            }

            return JsonSerializer.Deserialize<string[]>(responseMessage) ?? Array.Empty<string>();
        }
    }
}
