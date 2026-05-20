using System;
using MSMQ.Messaging;
using Xunit;

namespace Hangfire.SqlServer.Msmq.Tests
{
    internal sealed class MsmqFactAttribute : FactAttribute
    {
        private static readonly Lazy<bool> MsmqInstalled = new Lazy<bool>(IsMsmqInstalled);

        public MsmqFactAttribute()
        {
            if (!MsmqInstalled.Value)
            {
                Skip = "MSMQ is not installed on this machine.";
            }
        }

        private static bool IsMsmqInstalled()
        {
            try
            {
                MessageQueue.Exists(@".\Private$\hangfire-msmq-probe");
                return true;
            }
            catch (InvalidOperationException ex)
                when (ex.Message.IndexOf("Message Queuing has not been installed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
        }
    }
}
