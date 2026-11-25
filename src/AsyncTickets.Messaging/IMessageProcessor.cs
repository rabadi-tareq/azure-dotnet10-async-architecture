// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

namespace AsyncTickets.Messaging;

public interface IMessageProcessor : IAsyncDisposable
{
    Task StopAsync(CancellationToken cancellationToken);
}
