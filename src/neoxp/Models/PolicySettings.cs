// Copyright (C) 2015-2023 The Neo Project.
//
// PolicySettings.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace NeoExpress.Models
{
    enum PolicySettings
    {
        GasPerBlock,
        MinimumDeploymentFee,
        CandidateRegistrationFee,
        OracleRequestFee,
        NetworkFeePerByte,
        StorageFeeFactor,
        ExecutionFeeFactor
    }
}
