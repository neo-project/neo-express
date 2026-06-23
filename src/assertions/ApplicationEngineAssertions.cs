// Copyright (C) 2015-2026 The Neo Project.
//
// ApplicationEngineAssertions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Neo.SmartContract;
using Neo.VM;

namespace Neo.Assertions
{
    public class ApplicationEngineAssertions : ReferenceTypeAssertions<ApplicationEngine, ApplicationEngineAssertions>
    {
        public ApplicationEngineAssertions(ApplicationEngine subject) : base(subject)
        {
        }

        protected override string Identifier => nameof(ApplicationEngine);

        public AndConstraint<ApplicationEngineAssertions> Halt(string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(Subject.State == VMState.HALT)
                .FailWith("Expected {context:ApplicationEngine} to HALT{reason}, but found {0}{1}.",
                    Subject.State,
                    Subject.FaultException is null
                        ? string.Empty
                        : $" with exception \"{Subject.FaultException.GetBaseException().Message}\"");

            return new AndConstraint<ApplicationEngineAssertions>(this);
        }

        public AndConstraint<ApplicationEngineAssertions> Fault(string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(Subject.State == VMState.FAULT)
                .FailWith("Expected {context:ApplicationEngine} to FAULT{reason}, but found {0}.", Subject.State);

            return new AndConstraint<ApplicationEngineAssertions>(this);
        }
    }
}
