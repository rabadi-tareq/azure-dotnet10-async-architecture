// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;

namespace AsyncTickets.TicketRenderer.Models;

internal class AzureStorageOptions : IValidatableObject
{
    // Required unless ConnectionString is set.
    public string? Uri { get; set; }

    // Optional. When set (for example Azurite), it is used instead of Uri and the token credential.
    public string? ConnectionString { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Uri) && string.IsNullOrWhiteSpace(ConnectionString))
        {
            yield return new ValidationResult("Either Uri or ConnectionString must be set.", [nameof(Uri)]);
        }
    }

    [Required]
    public string? Container { get; set; }
}
