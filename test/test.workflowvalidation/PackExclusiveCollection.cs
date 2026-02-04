// Copyright (C) 2015-2026 The Neo Project.
//
// PackExclusiveCollection.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Xunit;

namespace test.workflowvalidation;
#if DISABLE_XUNIT_PARALLEL
[CollectionDefinition("PackExclusive", DisableParallelization = true)]
public sealed class PackExclusiveCollection { }
#else
[CollectionDefinition("PackExclusive", DisableParallelization = false)]
public sealed class PackExclusiveCollection { }
#endif
