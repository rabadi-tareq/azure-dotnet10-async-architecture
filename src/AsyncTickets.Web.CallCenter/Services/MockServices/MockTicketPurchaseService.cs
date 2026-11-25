// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.TicketManagement;

namespace AsyncTickets.Web.CallCenter.Services.MockServices
{
    public class MockTicketPurchaseService : ITicketPurchaseService
    {
        public Task<PurchaseTicketsResult> PurchaseTicketAsync(PurchaseTicketsRequest request)
        {
            return Task.FromResult(new PurchaseTicketsResult
            {
                Status = PurchaseTicketsResultStatus.UnableToProcess
            });
        }
    }
}
