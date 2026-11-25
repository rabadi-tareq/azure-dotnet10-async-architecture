// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.TicketManagement;

namespace AsyncTickets.Web.Api.Services.TicketManagementService
{
    public interface ITicketManagementService
    {
        Task<CountAvailableTicketsResult> CountAvailableTicketsAsync(int concertId);
        Task<HaveTicketsBeenSoldResult> HaveTicketsBeenSoldAsync(int concertId);
        Task<ReserveTicketsResult> ReserveTicketsAsync(int concertId, string userId, int numberOfTickets, int customerId);
    }
}
