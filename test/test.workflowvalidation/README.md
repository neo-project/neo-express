# Workflow Validation Tests

This project contains comprehensive integration tests that replicate the exact functionality of the GitHub Actions workflow defined in `.github/workflows/test.yml`. These tests allow you to validate your changes locally before pushing to GitHub, ensuring that the CI/CD pipeline will pass.

## Overview

The workflow validation tests are organized into three main test classes:

### 1. WorkflowValidationTests
Core workflow validation that mirrors the GitHub Actions jobs:

- **Test01_FormatValidation**: Validates `dotnet format --verify-no-changes`
- **Test02_BuildValidation**: Validates `dotnet build --configuration Release`
- **Test03_UnitTestValidation**: Validates `dotnet test --configuration Release` (includes ALL RocksDB tests)
- **Test04_CompleteWorkflowValidation**: Complete end-to-end validation replicating GitHub Actions

### 2. NeoxpToolIntegrationTests
Tests the neoxp tool installation and basic commands:

- **Test01_BuildAndInstallNeoxpTool**: Builds and installs neoxp tool globally
- **Test02_CreateCommand**: Tests `neoxp create` command
- **Test03_WalletCreateCommand**: Tests `neoxp wallet create bob` command
- **Test04_CheckpointCreateCommand**: Tests `neoxp checkpoint create` command

### 3. NeoxpAdvancedIntegrationTests
Advanced neoxp tool functionality tests:

- **Test01_PolicyCommands**: Tests `neoxp policy get/sync` commands
- **Test02_TransferCommandsOffline**: Tests offline transfer commands
- **Test03_RunCommandWithTimeout**: Tests `neoxp run` with timeout simulation

## Running the Tests

### Prerequisites

- .NET 9.0 SDK installed
- Git repository with neo-express solution
- All dependencies restored (`dotnet restore`)

### Run All Workflow Validation Tests

```bash
# Run all workflow validation tests
dotnet test test/test.workflowvalidation/test.workflowvalidation.csproj --configuration Release --verbosity normal

# Run only basic workflow tests (fastest)
dotnet test test/test.workflowvalidation/test.workflowvalidation.csproj --configuration Release --filter "FullyQualifiedName~WorkflowValidationTests"

# Run only neoxp tool tests
dotnet test test/test.workflowvalidation/test.workflowvalidation.csproj --configuration Release --filter "FullyQualifiedName~NeoxpToolIntegrationTests"

# Run only advanced integration tests
dotnet test test/test.workflowvalidation/test.workflowvalidation.csproj --configuration Release --filter "FullyQualifiedName~NeoxpAdvancedIntegrationTests"
```

### Run Specific Tests

```bash
# Run only format validation
dotnet test test/test.workflowvalidation/test.workflowvalidation.csproj --configuration Release --filter "Test01_FormatValidation"

# Run only build validation
dotnet test test/test.workflowvalidation/test.workflowvalidation.csproj --configuration Release --filter "Test02_BuildValidation"

# Run only unit test validation (includes RocksDB tests)
dotnet test test/test.workflowvalidation/test.workflowvalidation.csproj --configuration Release --filter "Test03_UnitTestValidation"

# Run complete workflow validation
dotnet test test/test.workflowvalidation/test.workflowvalidation.csproj --configuration Release --filter "Test04_CompleteWorkflowValidation"
```

## Test Execution Time

- **Test01_FormatValidation**: ~5-10 seconds (format check)
- **Test02_BuildValidation**: ~30-60 seconds (builds entire solution)
- **Test03_UnitTestValidation**: ~15-20 seconds (includes ALL RocksDB tests - 225+ tests)
- **Test04_CompleteWorkflowValidation**: ~18-20 seconds (all three tests in sequence)

**Total core workflow validation**: ~18-20 seconds

## RocksDB Test Coverage ✅

The unit test validation includes **complete RocksDB test coverage**:

- ✅ **Checkpoint Operations**: All RocksDB checkpoint creation and validation tests
- ✅ **Database Operations**: Read/write operations, column family handling
- ✅ **Store Tests**: ReadOnlyStore and ReadWriteStore functionality
- ✅ **Cache Client Tests**: RocksDB cache client operations
- ✅ **Fixture Tests**: Database setup and teardown operations

**Total RocksDB Tests**: 225+ tests covering all RocksDB functionality

## What These Tests Validate

### ✅ Exact GitHub Actions Equivalence

These tests replicate the **exact same commands** used in the GitHub Actions workflow:

1. **Format Check**: `dotnet format neo-express.sln --verify-no-changes --no-restore --verbosity diagnostic`
2. **Build**: `dotnet build neo-express.sln --configuration Release --no-restore --verbosity normal`
3. **Test**: `dotnet test neo-express.sln --configuration Release --no-build --verbosity normal`
4. **Pack**: `dotnet pack neo-express.sln --configuration Release --output ./out --no-build --verbosity normal`
5. **Tool Install**: `dotnet tool install --add-source ./out --verbosity normal --global --prerelease neo.express`
6. **Tool Commands**: All neoxp commands from the workflow

### ✅ Environment Validation

- Solution file discovery across different test environments
- Temporary directory management and cleanup
- Process lifecycle management
- Error handling and detailed logging

### ✅ Output Validation

- Verifies exit codes match expected values
- Validates output contains expected success messages
- Checks for creation of expected files and directories
- Ensures tool installation and functionality

## Benefits

1. **🚀 Faster Feedback**: Validate changes locally before pushing
2. **💰 Cost Savings**: Reduce GitHub Actions minutes usage
3. **🔍 Better Debugging**: Detailed local logs and output
4. **⚡ Parallel Development**: Test without waiting for CI queue
5. **🛡️ Confidence**: Ensure CI will pass before pushing

## Integration with Development Workflow

### Before Committing
```bash
# Quick validation (format + build only)
dotnet test test/test.workflowvalidation/test.workflowvalidation.csproj --filter "Test01_FormatValidation or Test02_BuildValidation"
```

### Before Pushing
```bash
# Full validation (all tests)
dotnet test test/test.workflowvalidation/test.workflowvalidation.csproj --configuration Release
```

### CI/CD Integration
These tests can also be run in CI/CD pipelines as an additional validation layer or as a replacement for the GitHub Actions workflow in self-hosted environments.

## Troubleshooting

### Common Issues

1. **Solution Not Found**: Tests automatically search for `neo-express.sln` in parent directories
2. **Tool Already Installed**: Tests handle existing tool installations gracefully
3. **Network Issues**: Advanced tests may skip network-dependent operations
4. **Permissions**: Ensure you have permissions to install global tools

### Debugging

Tests use `ITestOutputHelper` for detailed logging. Run with `--verbosity normal` to see detailed output:

```bash
dotnet test test/test.workflowvalidation/test.workflowvalidation.csproj --configuration Release --verbosity normal --logger "console;verbosity=detailed"
```

## Architecture

The tests are designed with:

- **Robust Error Handling**: Graceful handling of various failure scenarios
- **Resource Cleanup**: Automatic cleanup of temporary files and processes
- **Environment Agnostic**: Works across different development environments
- **Detailed Logging**: Comprehensive output for debugging
- **Modular Design**: Each test class focuses on specific functionality

This ensures reliable and maintainable test execution across different environments and scenarios.
