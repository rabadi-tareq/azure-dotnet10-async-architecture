// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Web.Api.Services.TicketManagementService;

namespace AsyncTickets.Web.Api.Services.MockServices
{
    public class MockTicketImageService : ITicketImageService
    {
        public Task<Stream> GetTicketImagesAsync(string imageName)
        {
            return Task.FromResult(Stream.Null);
        }
    }
}