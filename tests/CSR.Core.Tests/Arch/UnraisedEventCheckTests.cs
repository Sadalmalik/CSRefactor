using static CSR.Core.Tests.Arch.SolutionChecksTests;

namespace CSR.Core.Tests.Arch
{
    public class UnraisedEventCheckTests
    {
        private const string Events = """
            public interface IEvent<T> { }
            public interface IHandler<T> { void Handle(in T evt); }
            public static class Bus { public static void Raise<T>(object owner, T evt) { } }

            public struct StartEvent : IEvent<object> { public float Time; }
            public struct GroupEvent : IEvent<object> { public int Type; }
            public struct UpdateEvent : IEvent<object> { public float Time; }
            public struct EndEvent : IEvent<object> { }
            public class TickEvent : IEvent<object> { }
            public abstract class BaseEvent : IEvent<object> { }
            public class RuleEvent<T> : IEvent<object> where T : new() { public static T Request() => new T(); }
            public class DamageRule : RuleEvent<DamageRule> { }

            public class Sender
            {
                public void Run()
                {
                    Bus.Raise(this, new StartEvent { Time = 1 });
                    Bus.Raise<EndEvent>(this, default);
                    TickEvent tick = new();
                    Bus.Raise(this, tick);
                    Bus.Raise(this, DamageRule.Request());
                }
            }

            public class Connector : IHandler<StartEvent>, IHandler<GroupEvent>
            {
                public void Handle(in StartEvent evt) { }

                public void Handle(in GroupEvent evt) => _ = evt.Type;
            }
            """;

        private const string Door = """
            using System;

            public class Door
            {
                public event Action Opened;
                public event Action Closed;
                public event Action Locked;
                public event Action Unused;

                public void Open() => Opened?.Invoke();

                public void Close() { Closed(); }
            }

            public class Watcher
            {
                public Watcher(Door door)
                {
                    door.Opened += OnChanged;
                    door.Closed += OnChanged;
                    door.Locked += OnChanged;
                    door.Locked -= OnChanged;
                }

                private void OnChanged() { }
            }
            """;

        [Test]
        public void EventType_NotCreated_Found()
        {
            var findings = Analyze([Compile("Game", "Events.cs", Events)], "unraised-event");
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 6, 7 }));
            const string group = "событие 'GroupEvent' никто не поднимает; обрабатывают: Connector";
            Assert.That(findings[0].Message, Is.EqualTo(group));
            Assert.That(findings[1].Message, Is.EqualTo("событие 'UpdateEvent' никто не поднимает и не обрабатывает"));
        }

        [Test]
        public void EventType_OtherBase_NoFinding()
        {
            var findings = Analyze(
                [Compile("Game", "Events.cs", Events)],
                "unraised-event",
                settings => settings.Set("eventTypes", new[] { "IMessage" }));
            Assert.That(findings, Is.Empty);
        }

        [Test]
        public void FieldEvent_SubscribedNotRaised_Found()
        {
            var finding = Analyze([Compile("Game", "Door.cs", Door)], "unraised-event").Single();
            Assert.That(finding.Line, Is.EqualTo(7));
            Assert.That(finding.Message, Is.EqualTo("событие 'Door.Locked' нигде не поднимается; подписок: 2"));
        }

        [Test]
        public void FieldEvent_Disabled_NoFinding()
        {
            var findings = Analyze(
                [Compile("Game", "Door.cs", Door)],
                "unraised-event",
                settings => settings.Set("fieldEvents", false));
            Assert.That(findings, Is.Empty);
        }
    }
}
