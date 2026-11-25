// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.Search;

namespace AsyncTickets.Models.Services
{
    public interface IConcertSearchService
    {
        Task<SearchResponse<ConcertSearchResult>> SearchAsync(SearchRequest request);
        Task<ICollection<string>> SuggestAsync(string query);
    }
}
