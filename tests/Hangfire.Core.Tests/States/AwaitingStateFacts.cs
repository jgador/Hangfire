using System;
using Hangfire.Common;
using Hangfire.States;
using Xunit;

namespace Hangfire.Core.Tests.States
{
    public class AwaitingStateFacts
    {
        [Fact]
        public void StateName_IsEqualToAwaiting()
        {
            Assert.Equal("Awaiting", AwaitingState.StateName);
        }

        [Fact]
        public void NameProperty_ReturnsStateName()
        {
            var state = CreateState();
            Assert.Equal(AwaitingState.StateName, state.Name);
        }

        [DataCompatibilityRangeFact]
        public void SerializeData_ReturnsCorrectData()
        {
            var state = CreateState();

            var data = state.SerializeData();

            Assert.Equal(state.ParentId, data["ParentId"]);
            Assert.Equal("{\"$type\":\"Hangfire.States.EnqueuedState, Hangfire.Core\",\"Queue\":\"default\"}", data["NextState"]);
            Assert.Equal(state.Options.ToString("D"), data["Options"]);
            Assert.False(data.ContainsKey("Expiration"));
        }

        [Fact]
        public void IsFinal_ReturnsFalse()
        {
            var state = CreateState();
            Assert.False(state.IsFinal);
        }

        [Fact]
        public void IgnoreExceptions_ReturnsFalse()
        {
            var state = CreateState();
            Assert.False(state.IgnoreJobLoadException);
        }

        [DataCompatibilityRangeFact]
        public void JsonSerialize_ReturnsCorrectString()
        {
            var state = new AwaitingState("parent");

            var serialized = SerializationHelper.Serialize<IState>(state, SerializationOption.TypedInternal);

            Assert.Equal(
                "{\"$type\":\"Hangfire.States.AwaitingState, Hangfire.Core\",\"ParentId\":\"parent\",\"NextState\":{\"$type\":\"Hangfire.States.EnqueuedState, Hangfire.Core\",\"Queue\":\"default\"}}",
                serialized);
        }

        [DataCompatibilityRangeFact]
        public void JsonDeserialize_CanHandlePreviousFormat()
        {
            var json = "{\"$type\":\"Hangfire.States.AwaitingState, Hangfire.Core\",\"ParentId\":\"parent\",\"NextState\":{\"$type\":\"Hangfire.States.EnqueuedState, Hangfire.Core\",\"Queue\":\"default\"},\"Options\":1,\"Name\":\"Awaiting\"}";
            var state = SerializationHelper.Deserialize<AwaitingState>(json, SerializationOption.TypedInternal);

            Assert.Equal("parent", state.ParentId);
            Assert.Equal("Enqueued", state.NextState.Name);
        }

        [DataCompatibilityRangeFact]
        public void JsonDeserialize_CanHandleNewFormat()
        {
            var json = "{\"$type\":\"Hangfire.States.AwaitingState, Hangfire.Core\",\"ParentId\":\"parent\",\"NextState\":{\"$type\":\"Hangfire.States.EnqueuedState, Hangfire.Core\",\"Queue\":\"default\"}}";
            var state = SerializationHelper.Deserialize<AwaitingState>(json, SerializationOption.TypedInternal);

            Assert.Equal("parent", state.ParentId);
            Assert.Null(state.Reason);
            Assert.Equal("Enqueued", state.NextState.Name);
        }

        private static AwaitingState CreateState()
        {
            return new AwaitingState("1", new EnqueuedState(), JobContinuationOptions.OnlyOnSucceededState, TimeSpan.FromDays(1));
        }
    }
}
