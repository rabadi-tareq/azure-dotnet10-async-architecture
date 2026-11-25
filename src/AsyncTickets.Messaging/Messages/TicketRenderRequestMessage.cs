// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.ConcertContext;

namespace AsyncTickets.Messaging.Messages;

public record TicketRenderRequestMessage(Guid MessageId, Ticket Ticket, string? OutputPath, DateTime CreationTime);
