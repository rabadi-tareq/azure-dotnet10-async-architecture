// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.TicketManagement.Payment;

namespace AsyncTickets.Web.Api.Services.PaymentGatewayService
{
    public class PreAuthPaymentRequest
    {
        public double Amount { get; set; }
        public PaymentDetails? PaymentDetails { get; set; }
        public IDictionary<int, int>? Tickets { get; set; }
    }
}
