// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.TicketManagement;

namespace AsyncTickets.Web.CallCenter.Services
{
    public interface ITicketPurchaseService
    {
        Task<PurchaseTicketsResult> PurchaseTicketAsync(PurchaseTicketsRequest request);
    }
}