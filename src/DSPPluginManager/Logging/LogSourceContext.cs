using System;

namespace DSPPluginManager.Logging
{
    internal sealed class LogSourceContext
    {
        internal LogSourceContext(
            LogSourceKind kind,
            string identifier,
            string displayName
        )
        {
            Kind = kind;
            Identifier = RequireLabel(identifier, "identifier");
            DisplayName = RequireLabel(displayName, "display name");
        }

        internal LogSourceKind Kind { get; }

        internal string Identifier { get; }

        internal string DisplayName { get; }

        private static string RequireLabel(string value, string role)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Log source " + role + " is required.",
                    role
                );
            }
            foreach (char character in value)
            {
                if (char.IsControl(character))
                {
                    throw new ArgumentException(
                        "Log source " + role +
                        " cannot contain control characters.",
                        role
                    );
                }
            }
            return value;
        }
    }
}
