// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;

namespace AsyncTickets.Messaging;

public class MessageBusOptions : IValidatableObject
{
    // Required unless ConnectionString is set.
    public string? Host { get; set; }

    // Optional. When set (for example with the local emulator), it is used instead of Host and the token credential.
    public string? ConnectionString { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Host) && string.IsNullOrWhiteSpace(ConnectionString))
        {
            yield return new ValidationResult("Either Host or ConnectionString must be set.", [nameof(Host)]);
        }
    }

    [Required]
    public string? RenderRequestQueueName { get; set; }

    // This property is only required if messages should be generated
    // when ticket images are produced.
    public string? RenderCompleteQueueName { get; set; }

    public int MaxRetries { get; set; } = 3;

    public double BaseDelaySecondsBetweenRetries { get; set; } = 0.8;

    public double MaxDelaySeconds { get; set; } = 60;

    public double TryTimeoutSeconds { get; set; } = 60;
}
