using Microsoft.AspNetCore.Mvc;

namespace Mahkias.Api.Helpers
{
    public static class ControllerActor
    {
        public static string ActorName(this ControllerBase controller)
        {
            return Header(controller, "X-User-Name", 200);
        }

        public static string ActorEmail(this ControllerBase controller)
        {
            return Header(controller, "X-User-Email", 320);
        }

        private static string Header(ControllerBase controller, string name, int maxLength)
        {
            if (controller?.Request == null || !controller.Request.Headers.TryGetValue(name, out var values))
            {
                return null;
            }

            var raw = values.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(raw.Trim());
            }
            catch (UriFormatException)
            {
                decoded = raw.Trim();
            }

            if (string.IsNullOrWhiteSpace(decoded))
            {
                return null;
            }

            return decoded.Length <= maxLength ? decoded : decoded.Substring(0, maxLength);
        }
    }
}
