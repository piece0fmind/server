﻿using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class ServiceAccountCreateRequestModel
{
    [Required]
    [EncryptedString]
    public string Name { get; set; }

    public ServiceAccount ToServiceAccount(Guid organizationId)
    {
        return new ServiceAccount()
        {
            OrganizationId = organizationId,
            Name = Name,
        };
    }
}
