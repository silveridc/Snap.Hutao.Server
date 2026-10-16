using Microsoft.AspNetCore.Authorization.Policy;

namespace server.Services.Authorization
{
    // Singleton
    public class ResponseAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

        /// <inheritdoc/>
        public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Succeeded)
            {
                return defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            }

            return ForbidAsync(context);
        }

        private static async Task ForbidAsync(HttpContext context)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
            }

            Response response = Response.FailResponse(ReturnCode.LoginFail, "Please log in or register a Snap Hutao account first", ServerKeys.ServerPassportLoginRequired);
            await context.Response.WriteAsJsonAsync(response).ConfigureAwait(false);
        }
    }
}