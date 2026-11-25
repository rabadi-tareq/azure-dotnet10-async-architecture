// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.TicketManagement.Payment;

namespace AsyncTickets.Models.TicketManagement
{
    public class PurchaseTicketsRequest
    {
        public string? UserId { get; set; }
        public PaymentDetails? PaymentDetails { get; set; }
        public IDictionary<int, int>? ConcertIdsAndTicketCounts { get; set; }
    }
}
