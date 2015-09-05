using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using NLog.Config;
using NLog.Layouts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NLog.Etw.Tests
{
    public class EtwExtendedTargetTest
    {
        class ExtendedEtwEvent
        {
            public TraceEventLevel Level { get; set; }

            public int EventId { get; set; }

            public String LoggerName { get; set; }

            public String Message { get; set; }

            public override bool Equals(object obj) {
                if (obj == null)
                    return false;
                if (obj == this)
                    return true;
                var ev = obj as ExtendedEtwEvent;
                if (ev == null)
                    return false;
                return ev.Level == this.Level && ev.Message.Equals(this.Message, StringComparison.Ordinal)
                        && ev.LoggerName.Equals(this.LoggerName, StringComparison.Ordinal) && ev.EventId == this.EventId;
            }

            public override int GetHashCode() {
                return Message.GetHashCode();
            }
        }

        private readonly NLogEtwExtendedTarget etwTarget;

        public EtwExtendedTargetTest() {
            // setup NLog configuration
            var loggingConfiguration = new LoggingConfiguration();
            this.etwTarget = new NLogEtwExtendedTarget() { Layout = Layout.FromString("${uppercase:${level}}|${logger}|${message}") };

            loggingConfiguration.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, this.etwTarget));
            loggingConfiguration.AddTarget("etw", this.etwTarget);
            LogManager.Configuration = loggingConfiguration;
        }

        [Fact]
        public void Writing_Message_To_Etw() {
            var collectedEvents = new List<ExtendedEtwEvent>(5);
            var session = new TraceEventSession("SimpleMonitorSession");
            var eventSourceGuid = TraceEventProviders.GetEventSourceGuidFromName("LowLevelDesign-NLogEtwSource");
            Task.Run(() => {
                var parser = new DynamicTraceEventParser(session.Source);
                parser.All += (data) =>
                {
                    collectedEvents.Add(new ExtendedEtwEvent {
                        EventId = (int)data.ID,
                        Level = data.Level,
                        LoggerName = (String)data.PayloadByName("LoggerName"),
                        Message = (String)data.PayloadByName("Message")
                    });
                    if (collectedEvents.Count >= 5) {
                        session.Stop();
                    }
                };
            });

            session.EnableProvider(eventSourceGuid);

            var logger = LogManager.GetLogger("A");
            logger.Debug("test-debug");
            logger.Info("test-info");
            logger.Warn("test-warn");
            logger.Error("test-error");
            logger.Fatal("test-fatal");

            session.Source.Process();
            session.Stop();

            // assert collected events
            var expectedEvents = new ExtendedEtwEvent[] {
                new ExtendedEtwEvent { EventId = 1, LoggerName = "A", Level = TraceEventLevel.Verbose, Message = "DEBUG|A|test-debug" },
                new ExtendedEtwEvent { EventId = 2, LoggerName = "A", Level = TraceEventLevel.Informational, Message = "INFO|A|test-info" },
                new ExtendedEtwEvent { EventId = 3, LoggerName = "A", Level = TraceEventLevel.Warning, Message = "WARN|A|test-warn" },
                new ExtendedEtwEvent { EventId = 4, LoggerName = "A", Level = TraceEventLevel.Error, Message = "ERROR|A|test-error" },
                new ExtendedEtwEvent { EventId = 5, LoggerName = "A", Level = TraceEventLevel.Critical, Message = "FATAL|A|test-fatal" }
            };
            Assert.Equal(collectedEvents, expectedEvents);
        }
    }
} 