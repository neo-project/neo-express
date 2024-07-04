// Copyright (C) 2015-2024 The Neo Project.
//
// CodeCoverageDataCollector.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Neo.Collector.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Neo.Collector
{
    [DataCollectorFriendlyName("Neo code coverage")]
    [DataCollectorTypeUri("datacollector://Neo/ContractCodeCoverage/1.0")]
    public partial class CodeCoverageDataCollector : DataCollector, ITestExecutionEnvironmentSpecifier
    {
        const string COVERAGE_PATH_ENV_NAME = "NEO_TEST_APP_ENGINE_COVERAGE_PATH";

        readonly string coveragePath;

        CodeCoverageCollector? collector;
        DataCollectionEvents? events;
        DataCollectionSink? dataSink;
        DataCollectionEnvironmentContext? environmentContext;
        ILogger? logger;
        bool verboseLogConfig;
        IReadOnlyList<(string path, string name)>? debugInfoConfig;

        public CodeCoverageDataCollector()
        {
            do
            {
                coveragePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(coveragePath));
        }

        public override void Initialize(
                XmlElement? configurationElement,
                DataCollectionEvents events,
                DataCollectionSink dataSink,
                DataCollectionLogger logger,
                DataCollectionEnvironmentContext? environmentContext)
        {
            if (configurationElement is null)
                throw new ArgumentNullException(nameof(configurationElement));
            if (environmentContext is null)
                throw new ArgumentNullException(nameof(environmentContext));

            var verboseLogNode = configurationElement.SelectSingleNode("VerboseLog");
            verboseLogConfig = verboseLogNode is not null && bool.TryParse(verboseLogNode.InnerText, out var value) && value;
            debugInfoConfig = configurationElement.SelectNodes("DebugInfo")
                .OfType<XmlElement>()
                .Select(node =>
                {
                    var name = node.HasAttribute("name") ? node.GetAttribute("name") : string.Empty;
                    return (node.InnerText, name);
                })
                .ToList();

            this.events = events;
            this.dataSink = dataSink;
            this.environmentContext = environmentContext;
            this.logger = new Logger(logger, environmentContext);

            events.SessionStart += OnSessionStart;
            events.SessionEnd += OnSessionEnd;
            collector = new CodeCoverageCollector(this.logger, verboseLogConfig);

            if (verboseLogConfig)
                this.logger.LogWarning($"Initialize {this.coveragePath}");
        }

        protected override void Dispose(bool disposing)
        {
            if (events is not null)
            {
                events.SessionStart -= OnSessionStart;
                events.SessionEnd -= OnSessionEnd;
            }
            base.Dispose(disposing);
        }

        public IEnumerable<KeyValuePair<string, string>> GetTestExecutionEnvironmentVariables()
        {
            yield return new KeyValuePair<string, string>(COVERAGE_PATH_ENV_NAME, coveragePath);
        }

        void OnSessionStart(object sender, SessionStartEventArgs e)
        {
            foreach (var (path, name) in debugInfoConfig ?? Enumerable.Empty<(string, string)>())
            {
                collector?.LoadDebugInfoSetting(path, name);
            }

            foreach (var testSource in e.GetPropertyValue<IList<string>>("TestSources") ?? Enumerable.Empty<string>())
            {
                collector?.LoadTestSource(testSource);
            }
        }

        void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
            collector?.LoadCoverageFiles(coveragePath);
            var coverage = collector?.CollectCoverage().ToList();

            if (coverage is not null)
            {
                new CoberturaFormat().WriteReport(coverage, WriteAttachment);
                new RawCoverageFormat().WriteReport(coverage, WriteAttachment);
            }

            void WriteAttachment(string filename, Action<Stream> writeAttachment)
            {
                try
                {
                    if (environmentContext is null)
                        throw new NullReferenceException($"Invalid {nameof(environmentContext)}");
                    if (dataSink is null)
                        throw new NullReferenceException($"Invalid {nameof(dataSink)}");

                    var path = Path.Combine(coveragePath, filename);
                    using (var stream = File.OpenWrite(path))
                    {
                        writeAttachment(stream);
                        stream.Flush();
                    }
                    dataSink.SendFileAsync(environmentContext.SessionDataCollectionContext, path, false);
                }
                catch (Exception ex)
                {
                    logger?.LogError($"{coveragePath}\n{ex.Message}\n{ex.StackTrace}", ex);
                }
            }
        }
    }
}
