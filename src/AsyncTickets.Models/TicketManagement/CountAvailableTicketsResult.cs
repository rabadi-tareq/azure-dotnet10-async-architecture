// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.Services;

namespace AsyncTickets.Models.TicketManagement
{
    public class CountAvailableTicketsResult : IServiceProviderResult
    {
        public int CountOfAvailableTickets { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
