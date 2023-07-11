﻿using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(ServiceAccountsController))]
[SutProviderCustomize]
[JsonDocumentCustomize]
public class ServiceAccountsControllerTests
{
    [Theory]
    [BitAutoData]
    public async void GetServiceAccountsByOrganization_ReturnsEmptyList(
        SutProvider<ServiceAccountsController> sutProvider, Guid id)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        var result = await sutProvider.Sut.ListByOrganizationAsync(id);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>());

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void GetServiceAccountsByOrganization_Success(SutProvider<ServiceAccountsController> sutProvider,
        ServiceAccount resultServiceAccount)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<IServiceAccountRepository>().GetManyByOrganizationIdAsync(default, default, default)
            .ReturnsForAnyArgs(new List<ServiceAccount> { resultServiceAccount });

        var result = await sutProvider.Sut.ListByOrganizationAsync(resultServiceAccount.OrganizationId);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(resultServiceAccount.OrganizationId)),
                Arg.Any<Guid>(), Arg.Any<AccessClientType>());
        Assert.NotEmpty(result.Data);
        Assert.Single(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void GetServiceAccountsByOrganization_AccessDenied_Throws(
        SutProvider<ServiceAccountsController> sutProvider, Guid orgId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.ListByOrganizationAsync(orgId));
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccount_NoAccess_Throws(SutProvider<ServiceAccountsController> sutProvider,
        ServiceAccountCreateRequestModel data, Guid organizationId)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToServiceAccount(organizationId),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        var resultServiceAccount = data.ToServiceAccount(organizationId);
        sutProvider.GetDependency<ICreateServiceAccountCommand>().CreateAsync(default, default)
            .ReturnsForAnyArgs(resultServiceAccount);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(organizationId, data));
        await sutProvider.GetDependency<ICreateServiceAccountCommand>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<ServiceAccount>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccount_Success(SutProvider<ServiceAccountsController> sutProvider,
        ServiceAccountCreateRequestModel data, Guid organizationId)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToServiceAccount(organizationId),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        var resultServiceAccount = data.ToServiceAccount(organizationId);
        sutProvider.GetDependency<ICreateServiceAccountCommand>().CreateAsync(default, default)
            .ReturnsForAnyArgs(resultServiceAccount);

        await sutProvider.Sut.CreateAsync(organizationId, data);
        await sutProvider.GetDependency<ICreateServiceAccountCommand>().Received(1)
            .CreateAsync(Arg.Any<ServiceAccount>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void UpdateServiceAccount_NoAccess_Throws(SutProvider<ServiceAccountsController> sutProvider,
        ServiceAccountUpdateRequestModel data, ServiceAccount existingServiceAccount)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToServiceAccount(existingServiceAccount.Id),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(existingServiceAccount.Id)
            .ReturnsForAnyArgs(existingServiceAccount);
        var resultServiceAccount = data.ToServiceAccount(existingServiceAccount.Id);
        sutProvider.GetDependency<IUpdateServiceAccountCommand>().UpdateAsync(default)
            .ReturnsForAnyArgs(resultServiceAccount);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(existingServiceAccount.Id, data));
        await sutProvider.GetDependency<IUpdateServiceAccountCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ServiceAccount>());
    }

    [Theory]
    [BitAutoData]
    public async void UpdateServiceAccount_Success(SutProvider<ServiceAccountsController> sutProvider,
        ServiceAccountUpdateRequestModel data, ServiceAccount existingServiceAccount)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToServiceAccount(existingServiceAccount.Id),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        var resultServiceAccount = data.ToServiceAccount(existingServiceAccount.Id);
        sutProvider.GetDependency<IUpdateServiceAccountCommand>().UpdateAsync(default)
            .ReturnsForAnyArgs(resultServiceAccount);

        var result = await sutProvider.Sut.UpdateAsync(existingServiceAccount.Id, data);
        await sutProvider.GetDependency<IUpdateServiceAccountCommand>().Received(1)
            .UpdateAsync(Arg.Any<ServiceAccount>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateAccessToken_NoAccess_Throws(SutProvider<ServiceAccountsController> sutProvider,
        AccessTokenCreateRequestModel data, ServiceAccount serviceAccount, string mockClientSecret)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), serviceAccount,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        var resultAccessToken = data.ToApiKey(serviceAccount.Id);

        sutProvider.GetDependency<ICreateAccessTokenCommand>()
            .CreateAsync(default)
            .ReturnsForAnyArgs(new ApiKeyClientSecretDetails { ApiKey = resultAccessToken, ClientSecret = mockClientSecret });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateAccessTokenAsync(serviceAccount.Id, data));
        await sutProvider.GetDependency<ICreateAccessTokenCommand>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<ApiKey>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateAccessToken_Success(SutProvider<ServiceAccountsController> sutProvider,
        AccessTokenCreateRequestModel data, ServiceAccount serviceAccount, string mockClientSecret)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), serviceAccount,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        var resultAccessToken = data.ToApiKey(serviceAccount.Id);

        sutProvider.GetDependency<ICreateAccessTokenCommand>().CreateAsync(default)
            .ReturnsForAnyArgs(new ApiKeyClientSecretDetails { ApiKey = resultAccessToken, ClientSecret = mockClientSecret });

        await sutProvider.Sut.CreateAccessTokenAsync(serviceAccount.Id, data);
        await sutProvider.GetDependency<ICreateAccessTokenCommand>().Received(1)
            .CreateAsync(Arg.Any<ApiKey>());
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessTokens_NoAccess_Throws(SutProvider<ServiceAccountsController> sutProvider,
        ServiceAccount data, ICollection<ApiKey> resultApiKeys)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());

        foreach (var apiKey in resultApiKeys)
        {
            apiKey.Scope = "[\"api.secrets\"]";
        }

        sutProvider.GetDependency<IApiKeyRepository>().GetManyByServiceAccountIdAsync(default)
            .ReturnsForAnyArgs(resultApiKeys);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAccessTokens(data.Id));
        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByServiceAccountIdAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessTokens_Success(SutProvider<ServiceAccountsController> sutProvider, ServiceAccount data,
        ICollection<ApiKey> resultApiKeys)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());

        foreach (var apiKey in resultApiKeys)
        {
            apiKey.Scope = "[\"api.secrets\"]";
        }

        sutProvider.GetDependency<IApiKeyRepository>().GetManyByServiceAccountIdAsync(default)
            .ReturnsForAnyArgs(resultApiKeys);

        var result = await sutProvider.Sut.GetAccessTokens(data.Id);
        await sutProvider.GetDependency<IApiKeyRepository>().Received(1)
            .GetManyByServiceAccountIdAsync(Arg.Any<Guid>());
        Assert.NotEmpty(result.Data);
        Assert.Equal(resultApiKeys.Count, result.Data.Count());
    }

    [Theory]
    [BitAutoData]
    public async void RevokeAccessTokens_NoAccess_Throws(SutProvider<ServiceAccountsController> sutProvider,
        RevokeAccessTokensRequest data, ServiceAccount serviceAccount)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), serviceAccount,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RevokeAccessTokensAsync(serviceAccount.Id, data));
        await sutProvider.GetDependency<IRevokeAccessTokensCommand>().DidNotReceiveWithAnyArgs()
            .RevokeAsync(Arg.Any<ServiceAccount>(), Arg.Any<Guid[]>());
    }

    [Theory]
    [BitAutoData]
    public async void RevokeAccessTokens_Success(SutProvider<ServiceAccountsController> sutProvider,
        RevokeAccessTokensRequest data, ServiceAccount serviceAccount)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), serviceAccount,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());

        await sutProvider.Sut.RevokeAccessTokensAsync(serviceAccount.Id, data);
        await sutProvider.GetDependency<IRevokeAccessTokensCommand>().Received(1)
            .RevokeAsync(Arg.Any<ServiceAccount>(), Arg.Any<Guid[]>());
    }
}
