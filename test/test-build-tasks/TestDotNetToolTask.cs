// Copyright (C) 2015-2024 The Neo Project.
//
// TestDotNetToolTask.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Moq;
using Neo.BuildTasks;
using System;
using System.Linq;
using Xunit;

namespace build_tasks
{
    public class TestDotNetToolTask
    {
        class TestTask : DotNetToolTask
        {
            protected override string Command => "nccs";
            protected override string PackageId => "Neo.Compiler.CSharp";

            readonly Func<NugetPackageVersion, bool> validator;

            public TestTask(IProcessRunner processRunner, Func<NugetPackageVersion, bool> validator = null) : base(processRunner)
            {
                this.validator = validator;
            }

            protected override string GetArguments() => string.Empty;

            protected override bool ValidateVersion(NugetPackageVersion version)
                => validator is null || validator(version);
        }

        [Fact]
        public void contains_package()
        {
            var output = localOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Test the actual parsing - the mock data has "neo.compiler.csharp" (lowercase)
            // but the method should handle case-insensitive comparison
            var found = DotNetToolTask.ContainsPackage(output, "neo.compiler.csharp", out var version);

            Assert.True(found, "Should find neo.compiler.csharp package");
            Assert.Equal(new NugetPackageVersion(3, 3, 0), version);
        }

        [Fact]
        public void cant_find_valid_version()
        {
            Func<NugetPackageVersion, bool> validate = ver => false;

            var processRunner = MockProcRunner();
            var taskItem = new Mock<Microsoft.Build.Framework.ITaskItem>();
            taskItem.Setup(item => item.ItemSpec).Returns("fakePath");

            var task = new TestTask(processRunner.Object, validate);
            Assert.False(task.FindTool("neo.compiler.csharp", taskItem.Object, out var type, out var version));
        }

        [Fact]
        public void find_valid_global_version()
        {
            var expectedVersion = new NugetPackageVersion(3, 1, 0);
            Func<NugetPackageVersion, bool> validate = ver => ver == expectedVersion;

            var processRunner = MockProcRunner();
            var taskItem = new Mock<Microsoft.Build.Framework.ITaskItem>();
            taskItem.Setup(item => item.ItemSpec).Returns("fakePath");

            var task = new TestTask(processRunner.Object, validate);
            Assert.True(task.FindTool("neo.compiler.csharp", taskItem.Object, out var type, out var version));
            Assert.Equal(DotNetToolType.Global, type);
            Assert.Equal(expectedVersion, version);
        }


        [Fact]
        public void find_valid_local_version()
        {
            var expectedVersion = new NugetPackageVersion(3, 3, 0);
            Func<NugetPackageVersion, bool> validate = ver => true;

            var processRunner = MockProcRunner();
            var taskItem = new Mock<Microsoft.Build.Framework.ITaskItem>();
            taskItem.Setup(item => item.ItemSpec).Returns("fakePath");

            var task = new TestTask(processRunner.Object, validate);
            Assert.True(task.FindTool("neo.compiler.csharp", taskItem.Object, out var type, out var version));
            Assert.Equal(DotNetToolType.Local, type);
            Assert.Equal(expectedVersion, version);
        }

        [Fact]
        public void find_valid_prerel_version()
        {
            var expectedVersion = new NugetPackageVersion(3, 3, 1037, "storage-schema-preview");
            Func<NugetPackageVersion, bool> validate = ver => ver >= new NugetPackageVersion(3, 3, 0);

            var processRunner = MockProcRunner(sspOutput);
            var taskItem = new Mock<Microsoft.Build.Framework.ITaskItem>();
            taskItem.Setup(item => item.ItemSpec).Returns("fakePath");

            var task = new TestTask(processRunner.Object, validate);
            Assert.True(task.FindTool("neo.compiler.csharp", taskItem.Object, out var type, out var version));
            Assert.Equal(DotNetToolType.Local, type);
            Assert.Equal(expectedVersion, version);
        }

        static Mock<IProcessRunner> MockProcRunner(string local = localOutput, string global = globalOutput)
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.Run("dotnet", "tool list --local", It.IsAny<string>()))
                .Returns(new ProcessResults(0, local.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), Array.Empty<string>()));
            processRunner
                .Setup(r => r.Run("dotnet", "tool list --global", It.IsAny<string>()))
                .Returns(new ProcessResults(0, global.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), Array.Empty<string>()));
            return processRunner;
        }

        const string sspOutput = @"Package Id               Version                              Commands      Manifest
----------------------------------------------------------------------------------------------------------------------------------------------------------
neo.express              3.1.46                               neoxp         C:\Users\harry\Source\neo\seattle\samples\nft-sample\.config\dotnet-tools.json
neo.compiler.csharp      3.3.1037-storage-schema-preview      nccs          C:\Users\harry\Source\neo\seattle\samples\nft-sample\.config\dotnet-tools.json";

        const string localOutput = @"Package Id               Version            Commands             Manifest
-----------------------------------------------------------------------------------------------------------------------------------------------------
neo.express              3.3.7-preview      neoxp                C:\Users\harry\Source\neo\seattle\samples\registrar-sample\.config\dotnet-tools.json
neo.compiler.csharp      3.3.0              nccs                 C:\Users\harry\Source\neo\seattle\samples\registrar-sample\.config\dotnet-tools.json
neo.trace                3.3.7-preview      neotrace             C:\Users\harry\Source\neo\seattle\samples\registrar-sample\.config\dotnet-tools.json
neo.test.runner          3.3.4-preview      neo-test-runner      C:\Users\harry\Source\neo\seattle\samples\registrar-sample\.config\dotnet-tools.json";

        const string globalOutput = @"Package Id                Version            Commands
------------------------------------------------------------
devhawk.dumpnef           3.2.8-preview      dumpnef
dotnet-outdated-tool      4.1.0              dotnet-outdated
dotnet-script             1.3.1              dotnet-script
dotnet-t4                 2.2.1              t4
fornax                    0.14.0             fornax
nbgv                      3.4.255            nbgv
neo.compiler.csharp       3.1.0              nccs
neo.express               3.1.49             neoxp
sleet                     5.0.1              sleet";
    }
}
