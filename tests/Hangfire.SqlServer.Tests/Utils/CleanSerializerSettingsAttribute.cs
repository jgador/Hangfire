using System.Reflection;
using Hangfire.Common;
using Xunit.Sdk;

namespace Hangfire.SqlServer.Tests
{
    internal sealed class CleanSerializerSettingsAttribute : BeforeAfterTestAttribute
    {
        public override void Before(MethodInfo methodUnderTest)
        {
            ClearSettings();
        }

        public override void After(MethodInfo methodUnderTest)
        {
            ClearSettings();
        }

        private static void ClearSettings()
        {
#pragma warning disable 618
            JobHelper.SetSerializerOptions(null);
#pragma warning restore 618
            GlobalConfiguration.Configuration.UseSerializerOptions(null);
        }
    }
}
