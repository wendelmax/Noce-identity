using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Auth0.Core.Exceptions;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Auth0.ManagementApi.Paging;
using Microsoft.Extensions.Logging;
using NICE.Identity.Authorisation.WebAPI.Configuration;
using NICE.Identity.Authorisation.WebAPI.DataModels;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using User = NICE.Identity.Authorisation.WebAPI.DataModels.User;

namespace NICE.Identity.Authorisation.WebAPI.Services
{
	public class Auth0ManagementService : IProviderManagementService
    {
        private readonly ILogger<Auth0ManagementService> _logger;
        private readonly IManagementConnection _managementConnection;
        private readonly HttpClient _httpClient;
        private readonly AsyncRetryPolicy _retryPolicy;

		public Auth0ManagementService(ILogger<Auth0ManagementService> logger, IHttpClientFactory httpClientFactory, IManagementConnection managementConnection)
        {
	        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	        _httpClient = httpClientFactory.CreateClient();
	        _managementConnection = managementConnection;
	        _retryPolicy = GetAuth0RetryPolicy();
        }

        public async Task<string> GetAccessTokenForManagementAPI()
        {
	        try
	        {
		        using (var client = new AuthenticationApiClient(new Uri($"https://{AppSettings.ManagementAPI.Domain}/")))
		        {
			        var managementApiTokenRequest = new ClientCredentialsTokenRequest
			        {
				        ClientId = AppSettings.ManagementAPI.ClientId,
				        ClientSecret = AppSettings.ManagementAPI.ClientSecret,
				        Audience = AppSettings.ManagementAPI.ApiIdentifier
			        };

			        var managementApiToken = await client.GetTokenAsync(managementApiTokenRequest);

			        return managementApiToken.AccessToken;
		        }
	        }
	        catch (Exception e)
	        {
		        _logger.LogError(e.Message);
		        throw new Exception("Error in GetAccessTokenForManagementAPI when calling the Management API.", e);
	        }
        }

        public async Task UpdateUser(string authenticationProviderUserId, User user)
        {
            _logger.LogInformation($"Update user {authenticationProviderUserId} in auth0");

            var managementApiAccessToken = await GetAccessTokenForManagementAPI();
            try
            {
	            using (var managementApiClient = new ManagementApiClient(managementApiAccessToken, AppSettings.ManagementAPI.Domain, _managementConnection))
	            {
		            var userUpdateRequest = new UserUpdateRequest
		            {
			            Blocked = user.IsLockedOut,
			            EmailVerified = user.HasVerifiedEmailAddress,
			            Email = user.EmailAddress,
			            FirstName = user.FirstName,
			            LastName = user.LastName,
			            FullName = $"{user.FirstName} {user.LastName}"
		            };
		            await managementApiClient.Users.UpdateAsync(authenticationProviderUserId, userUpdateRequest);
	            }
            }
            catch (Exception e)
            {
	            _logger.LogError(e.Message);
	            throw new Exception("Error when calling the Management API.", e);
            }

        }

        public async Task DeleteUser(string authenticationProviderUserId)
        {
            _logger.LogInformation($"Delete user {authenticationProviderUserId} from auth0");

            var managementApiAccessToken = await GetAccessTokenForManagementAPI();

            try
            {
	            using (var managementApiClient = new ManagementApiClient(managementApiAccessToken, AppSettings.ManagementAPI.Domain, _managementConnection))
	            {
		            await managementApiClient.Users.DeleteAsync(authenticationProviderUserId);
	            }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw new Exception("Error when calling the Management API.", e);
            }
        }

		public async Task RevokeRefreshTokensForUser(string nameIdentifier)
        {
	        _logger.LogInformation($"Revoke Refresh Tokens For User {nameIdentifier}");

            var managementApiAccessToken = await GetAccessTokenForManagementAPI();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			GetAllRefreshTokensAndRevokeThem(nameIdentifier, managementApiAccessToken); //intentionally not awaiting so the logout succeeds, while refresh tokens are still deleted in the background.
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

		}

		private async Task GetAllRefreshTokensAndRevokeThem(string nameIdentifier, string managementApiAccessToken)
		{
			using (var managementApiClient = new ManagementApiClient(managementApiAccessToken, AppSettings.ManagementAPI.Domain, _managementConnection))
			{
				var pageNumber = 0;
				const int pageSize = 50;
				IPagedList<DeviceCredential> allDevices;

				var getDeviceCredentialsRequest = new Auth0.ManagementApi.Models.GetDeviceCredentialsRequest()
				{
					UserId = nameIdentifier,
					Type = "refresh_token"
				};

				do
				{
					var pagination = new PaginationInfo(pageNumber, pageSize);

					allDevices = await managementApiClient.DeviceCredentials.GetAllAsync(getDeviceCredentialsRequest, pagination);

					await RevokeRefreshToken(managementApiClient, allDevices.Select(device => device.Id));

					pageNumber++;
				} while (allDevices.Count >= pageSize);
			}
		}

        private async Task RevokeRefreshToken(ManagementApiClient managementApiClient, IEnumerable<string> refreshTokens)
        {
	        _logger.LogInformation($"Starting Revoking {refreshTokens.Count()} Refresh Tokens");

            foreach (var refreshToken in refreshTokens)
	        {
		        await _retryPolicy.ExecuteAsync(async () => await managementApiClient.DeviceCredentials.DeleteAsync(refreshToken));
		        _logger.LogInformation($"Refresh token: {refreshToken} revoked");
			}
            _logger.LogInformation($"Finished Revoking {refreshTokens.Count()} Refresh Tokens");
        }

        private Polly.Retry.AsyncRetryPolicy GetAuth0RetryPolicy()
        {
	        return Policy
		        .Handle<RateLimitApiException>(ex => ex.RateLimit.Remaining < 1 && ex.RateLimit.Reset.HasValue && ex.RateLimit.Reset.Value > DateTime.UtcNow)
		        .WaitAndRetryAsync(
			        retryCount: 3,
			        sleepDurationProvider: (retryCount, exception, context) => (((RateLimitApiException)exception).RateLimit.Reset.Value - DateTime.UtcNow),
			        onRetryAsync: (exception, timespan, retryNumber, context) => Task.Run(() => _logger.LogWarning($"RateLimit for management api hit. Retry attempt no: {retryNumber}. DateTime.UtcNow: {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss} Sleeping till: {((RateLimitApiException)exception).RateLimit.Reset.Value:dd/MM/yyyy HH:mm:ss} so sleeping for: {timespan.TotalSeconds} seconds"))
		        );
        }


		public async Task<(int totalUsersCount, List<BasicUserInfo> last10Users)> GetLastTenUsersAndTotalCount()
        {
	        _logger.LogInformation($"Getting users from management api");

	        var managementApiAccessToken = await GetAccessTokenForManagementAPI();
	        
	        try
	        {
		        using (var managementApiClient = new ManagementApiClient(managementApiAccessToken, AppSettings.ManagementAPI.Domain, _managementConnection))
		        {
			        var pagination = new PaginationInfo(pageNo: 0, perPage: 10, includeTotals: true);

			        const string sortByCreatedDescending = "created_at:-1";
			        var pagedUsers = await managementApiClient.Users.GetAllAsync(new GetUsersRequest {Sort = sortByCreatedDescending,}, pagination);

			        var last10Users = pagedUsers.Select(user => new BasicUserInfo(nameIdentifier: user.UserId, emailAddress: user.Email)).ToList();

			        return (pagedUsers.Paging.Total, last10Users);
		        }
	        }
	        catch (Exception e)
	        {
		        _logger.LogError(e.ToString());
		        throw new Exception("Error when calling the Management API.", e);
	        }
        }
    }


   

}