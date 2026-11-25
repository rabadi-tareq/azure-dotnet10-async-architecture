// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

namespace AsyncTickets.Web.CallCenter.Services.MockServices;

public class MockTicketImageService : ITicketImageService
{
    public Task<Stream> GetTicketImagesAsync(string imageName)
    {
        return Task.FromResult(new MemoryStream() as Stream);
    }
}