// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Web.Api.Services.TicketManagementService;

namespace AsyncTickets.Web.Api.Services.MockServices
{
    public class MockTicketRenderingService : ITicketRenderingService
    {
        public Task CreateTicketImageAsync(int ticketId)
        {
            return Task.CompletedTask;
        }
    }
}
