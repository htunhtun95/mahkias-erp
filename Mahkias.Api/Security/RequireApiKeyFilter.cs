using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mahkias.Api.Security
{
    public sealed class RequireApiKeyFilter : IAsyncActionFilter
    {
        public const string HeaderName = "X-Api-Key";

        private readonly byte[] _key;

        public RequireApiKeyFilter(IConfiguration configuration)
        {
            var key = configuration["ApiSecurity:Key"];
            _key = string.IsNullOrEmpty(key) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(key);
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
                || !Matches(provided.ToString()))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            await next();
        }

        private bool Matches(string provided)
        {
            var given = Encoding.UTF8.GetBytes(provided);
            return given.Length == _key.Length
                && _key.Length > 0
                && CryptographicOperations.FixedTimeEquals(given, _key);
        }
    }

    public sealed class RequireApiKeyAttribute : TypeFilterAttribute
    {
        public RequireApiKeyAttribute() : base(typeof(RequireApiKeyFilter))
        {
        }
    }
}
