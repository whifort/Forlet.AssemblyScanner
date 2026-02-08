# Changelog

All notable changes to Forlet.AssemblyScanner will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2026-02-08

### Added
- **FindTypeByName methods**: Added `matchTargetFullName` parameter to `FindTypeByNameImplementing()` and `FindTypeByNameDerivedFrom()` for independent control of target type name matching, separate from interface/base class name matching controlled by `ScanOptions.MatchFullName`.

### Changed
- **Documentation**: Updated API.md and USAGE.md with `matchTargetFullName` parameter documentation and usage examples.

### Testing
- Added 8 new tests covering decoupled name matching behavior.

## [1.0.0] - 2026-01-20

### Added

#### Core Scanning Engine
- **MetadataScanner**: Core functionality for type discovery using `MetadataLoadContext` for safe, non-executing inspection.
  - `FindTypesImplementing()`: Find types implementing specific interfaces.
  - `FindTypesDerivedFrom()`: Find types derived from base classes.
  - `FindTypeByNameImplementing()`: Find specific implementation with early-exit optimization.
  - `FindTypeByNameDerivedFrom()`: Find specific derived class with early-exit optimization.
  - Full support for generic types using backtick notation (e.g., `` ICommand`1 ``, `` IHandler`2 ``).
  - Configurable `ScanOptions` for namespace matching (`MatchFullName`), abstract class inclusion, and non-public type filtering.
  - Automatic exclusion of nested classes by default to improve scan relevance.

#### Build & Change Management
- **ProjectDllResolver**: Automated DLL resolution and project build management.
  - Support for multiple build strategies: `NoBuild`, `AutoBuild`, and `AlwaysBuild`.
  - Integrated `OnBuildStart` callbacks and `BuildOutput` tracking for enhanced debugging.
  - Support for both Debug and Release configuration detection.
- **StaleChecker**: High-performance staleness detection and change tracking.
  - Efficient directory timestamp comparison.
  - Optional file-level change detection (`CheckForEdit`) for precise staleness checks.
  - Configurable path scanning to optimize performance in large-scale projects.

#### Documentation & Quality Assurance
- **Documentation**: 
  - Main README featuring a quick-start guide and configuration snippets.
  - Detailed Usage Guide with best practices and API reference.
  - Comprehensive XML documentation for all public members.
  - Updated test suite README counts and ScanOptions examples (IncludeStructs/IncludeNestedTypes).
- **Testing**: 
  - Robust test suite containing 102 tests.
  - Full coverage for scanning logic, build management, and generic type handling.
  - Performance optimization verification and error handling validation.
- **Reliability**: 
  - Strict input validation and detailed error messaging.
  - Proper resource disposal patterns for `MetadataLoadContext` to prevent memory leaks.
  - Safe exception propagation and recovery.

---

[1.0.1]: https://github.com/whifort/Forlet.AssemblyScanner/releases/tag/v1.0.1
[1.0.0]: https://github.com/whifort/Forlet.AssemblyScanner/releases/tag/v1.0.0
