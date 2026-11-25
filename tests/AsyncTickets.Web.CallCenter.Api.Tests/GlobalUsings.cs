// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using NSubstitute;
global using AsyncTickets.Messaging;
global using AsyncTickets.Messaging.Messages;
global using AsyncTickets.Models.ConcertContext;
global using AsyncTickets.Web.Api.Services.SqlDatabaseConcertRepository;
global using AsyncTickets.Web.Api.Services.TicketManagementService;
global using AsyncTickets.Web.CallCenter.Api.Services.TicketManagementService;
global using Xunit;
