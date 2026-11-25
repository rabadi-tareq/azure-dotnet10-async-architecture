// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

namespace AsyncTickets.Messaging.Messages;

public record TicketRenderCompleteMessage(Guid MessageId, int TicketId, string OutputPath, DateTime CreationTime);
