// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

namespace AsyncTickets.Messaging;

public interface IMessageSender<T> : IAsyncDisposable
{
    Task PublishAsync(T message, CancellationToken cancellationToken);

    Task CloseAsync(CancellationToken cancellationToken);
}
