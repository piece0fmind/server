﻿using System.Security.Claims;
using System.Text.Json;
using Bit.Core;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

#nullable enable

namespace Bit.Identity.IntegrationTest.Endpoints;

public class IdentityServerSsoTests
{
    const string TestEmail = "sso_user@email.com";

    [Fact]
    public async Task Test_MasterPassword_DecryptionType()
    {
        // Arrange
        var challenge = new string('c', 50);
        var factory = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        }, challenge);

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "10" },
            { "deviceIdentifier", "test_id" },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }));

        // Assert
        // If the organization has a member decryption type of MasterPassword that should be the only option in the reply
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": true
        // }

        AssertHelper.AssertJsonProperty(userDecryptionOptions, "HasMasterPassword", JsonValueKind.True);

        // One property for the Object and one for master password
        Assert.Equal(2, userDecryptionOptions.EnumerateObject().Count());
    }

    [Fact]
    public async Task SsoLogin_TrustedDeviceEncryption_ReturnsOptions()
    {
        // Arrange
        var challenge = new string('c', 50);
        var factory = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
        }, challenge);

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "10" },
            { "deviceIdentifier", "test_id" },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }));

        // Assert
        // If the organization has selected TrustedDeviceEncryption but the user still has their master password
        // they can decrypt with either option
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": true,
        //   "TrustedDeviceOption": {
        //     "HasAdminApproval": false
        //   }
        // }

        // Should have master password & one for trusted device with admin approval
        AssertHelper.AssertJsonProperty(userDecryptionOptions, "HasMasterPassword", JsonValueKind.True);

        var trustedDeviceOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "TrustedDeviceOption", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasAdminApproval", JsonValueKind.False);
    }

    [Fact]
    public async Task SsoLogin_TrustedDeviceEncryption_WithAdminResetPolicy_ReturnsOptions()
    {
        // Arrange
        var challenge = new string('c', 50);
        var factory = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
        }, challenge);

        var database = factory.GetDatabaseContext();

        var organization = await database.Organizations.SingleAsync();

        var policyRepository = factory.Services.GetRequiredService<IPolicyRepository>();
        await policyRepository.CreateAsync(new Policy
        {
            Type = PolicyType.ResetPassword,
            Enabled = true,
            Data = "{\"autoEnrollEnabled\": false }",
            OrganizationId = organization.Id,
        });

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "10" },
            { "deviceIdentifier", "test_id" },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }));

        // Assert
        // If the organization has selected TrustedDeviceEncryption but the user still has their master password
        // they can decrypt with either option
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);

        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": true,
        //   "TrustedDeviceOption": {
        //     "HasAdminApproval": true
        //   }
        // }

        // Should have one item for master password & one for trusted device with admin approval
        AssertHelper.AssertJsonProperty(userDecryptionOptions, "HasMasterPassword", JsonValueKind.True);

        var trustedDeviceOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "TrustedDeviceOption", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasAdminApproval", JsonValueKind.True);
    }

    [Fact]
    public async Task SsoLogin_TrustedDeviceEncryptionAndNoMasterPassword_ReturnsOneOption()
    {
        // Arrange
        var challenge = new string('c', 50);
        var factory = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
        }, challenge);

        await UpdateUserAsync(factory, user => user.MasterPassword = null);

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "10" },
            { "deviceIdentifier", "test_id" },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }));

        // Assert
        // If the organization has selected TrustedDeviceEncryption but the user still has their master password
        // they can decrypt with either option
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": false,
        //   "TrustedDeviceOption": {
        //     "HasAdminApproval": true
        //   }
        // }

        var trustedDeviceOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "TrustedDeviceOption", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasAdminApproval", JsonValueKind.False);
    }

    [Fact]
    public async Task SsoLogin_TrustedDeviceEncryption_FlagTurnedOff_DoesNotReturnOption()
    {
        // Arrange
        var challenge = new string('c', 50);

        // This creates SsoConfig that HAS enabled trusted device encryption which should have only been
        // done with the feature flag turned on but we are testing that even if they have done that, this will turn off
        // if returning as an option if the flag has later been turned off.  We should be very careful turning the flag
        // back off.
        var factory = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
        }, challenge, trustedDeviceEnabled: false);

        await UpdateUserAsync(factory, user => user.MasterPassword = null);

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "10" },
            { "deviceIdentifier", "test_id" },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }));

        // Assert
        // If the organization has selected TrustedDeviceEncryption but the user still has their master password
        // they can decrypt with either option
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": false
        // }

        // Should only have 2 properties
        Assert.Equal(2, userDecryptionOptions.EnumerateObject().Count());
    }

    [Fact]
    public async Task SsoLogin_KeyConnector_ReturnsOptions()
    {
        // Arrange
        var challenge = new string('c', 50);
        var factory = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.KeyConnector,
            KeyConnectorUrl = "https://key_connector.com"
        }, challenge);

        await UpdateUserAsync(factory, user => user.MasterPassword = null);

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "10" },
            { "deviceIdentifier", "test_id" },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }));

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);

        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "KeyConnectorOption": {
        //     "KeyConnectorUrl": "https://key_connector.com"
        //   }
        // }

        var keyConnectorOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "KeyConnectorOption", JsonValueKind.Object);

        var keyConnectorUrl = AssertHelper.AssertJsonProperty(keyConnectorOption, "KeyConnectorUrl", JsonValueKind.String).GetString();
        Assert.Equal("https://key_connector.com", keyConnectorUrl);

        // For backwards compatibility reasons the url should also be on the root
        keyConnectorUrl = AssertHelper.AssertJsonProperty(root, "KeyConnectorUrl", JsonValueKind.String).GetString();
        Assert.Equal("https://key_connector.com", keyConnectorUrl);
    }

    private static async Task<IdentityApplicationFactory> CreateFactoryAsync(SsoConfigurationData ssoConfigurationData, string challenge, bool trustedDeviceEnabled = true)
    {
        var factory = new IdentityApplicationFactory();


        var authorizationCode = new AuthorizationCode
        {
            ClientId = "web",
            CreationTime = DateTime.UtcNow,
            Lifetime = (int)TimeSpan.FromMinutes(5).TotalSeconds,
            RedirectUri = "https://localhost:8080/sso-connector.html",
            RequestedScopes = new[] { "api", "offline_access" },
            CodeChallenge = challenge.Sha256(),
            CodeChallengeMethod = "plain", // 
            Subject = null, // Temporarily set it to null
        };

        factory.SubstitueService<IAuthorizationCodeStore>(service =>
        {
            service.GetAuthorizationCodeAsync("test_code")
                .Returns(authorizationCode);
        });

        factory.SubstitueService<IFeatureService>(service =>
        {
            service.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, Arg.Any<ICurrentContext>())
                .Returns(trustedDeviceEnabled);
        });

        // This starts the server and finalizes services
        var registerResponse = await factory.RegisterAsync(new RegisterRequestModel
        {
            Email = TestEmail,
            MasterPasswordHash = "master_password_hash",
        });

        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(TestEmail);

        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
        });

        var organizationUserRepository = factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var organizationUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        var ssoConfigRepository = factory.Services.GetRequiredService<ISsoConfigRepository>();
        await ssoConfigRepository.CreateAsync(new SsoConfig
        {
            OrganizationId = organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(ssoConfigurationData, JsonHelpers.CamelCase),
        });

        var subject = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtClaimTypes.Subject, user.Id.ToString()), // Get real user id
            new Claim(JwtClaimTypes.Name, TestEmail),
            new Claim(JwtClaimTypes.IdentityProvider, "sso"),
            new Claim("organizationId", organization.Id.ToString()),
            new Claim(JwtClaimTypes.SessionId, "SOMETHING"),
            new Claim(JwtClaimTypes.AuthenticationMethod, "external"),
            new Claim(JwtClaimTypes.AuthenticationTime, DateTime.UtcNow.AddMinutes(-1).ToEpochTime().ToString())
        }, "IdentityServer4", JwtClaimTypes.Name, JwtClaimTypes.Role));

        authorizationCode.Subject = subject;

        return factory;
    }

    private static async Task UpdateUserAsync(IdentityApplicationFactory factory, Action<User> changeUser)
    {
        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(TestEmail);

        changeUser(user);

        await userRepository.ReplaceAsync(user);
    }
}
