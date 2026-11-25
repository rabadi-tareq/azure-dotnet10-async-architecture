// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.ConcertContext;

namespace AsyncTickets.TicketRenderer.Services;

public interface IBarcodeGenerator
{
    IEnumerable<int> GenerateBarcode(Ticket ticket);
}
