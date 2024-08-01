// Copyright (C) 2015-2024 The Neo Project.
//
// CodeCoverageDataCollector.Logger.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using System;

namespace Neo.Collector
{
    public partial class CodeCoverageDataCollector
    {
        class Logger : ILogger
        {
            readonly DataCollectionLogger logger;
            readonly DataCollectionContext collectionContext;

            public Logger(DataCollectionLogger logger, DataCollectionEnvironmentContext environmentContext)
            {
                this.logger = logger;
                collectionContext = environmentContext.SessionDataCollectionContext;
            }

            public void LogError(string text, Exception? exception = null)
            {
                if (exception is null)
                    logger.LogError(collectionContext, text);
                else
                    logger.LogError(collectionContext, text, exception);
            }

            public void LogWarning(string text)
            {
                logger.LogWarning(collectionContext, text);
            }
        }
    }
}
