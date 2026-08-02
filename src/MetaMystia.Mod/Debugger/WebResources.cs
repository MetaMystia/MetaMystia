using System.IO;
using System.Reflection;

namespace MetaMystia.Debugger
{
    public static class WebResources
    {
        private static string _loginHtmlContent;
        private static string _htmlContent;

        public static string LoginHtmlContent
        {
            get
            {
                if (_loginHtmlContent == null)
                {
                    _loginHtmlContent = LoadEmbeddedResource("MetaMystia.Debugger.login.html");
                }
                return _loginHtmlContent;
            }
        }

        public static string HtmlContent
        {
            get
            {
                if (_htmlContent == null)
                {
                    _htmlContent = LoadEmbeddedResource("MetaMystia.Debugger.index.html");
                }
                return _htmlContent;
            }
        }

        private static string LoadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
            }
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
