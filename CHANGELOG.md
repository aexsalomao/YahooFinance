# Changelog

## [0.0.7] - 2024-06-24

### Fixed

- Added user agent. Yahoo started denying requests without a user agent, which prevented downloading data.

## [0.0.6] - 2023-05-04

### Changed

- Upper bound on FSharp.Data.Dependency

## [0.0.5] - 2023-01-30

### Changed
- Bump version

## [0.0.1] - 2023-01-30

### Changed
- Promote to full release.

## [0.0.1-alpha.4] - 2022-08-22

### Changed

- Removed time from downloaded date.

## [0.0.1-alpha.3] - 2022-08-22

### Changed
- Reformat changelog

## [0.0.1-alpha.2] - 2022-08-22

### Changed
- Reformat changelog

## [0.0.1-alpha.1] - 2022-08-22

### Added
- Added a Changelog
- Automated versioning.

### Changed
- Open/High/Low/Close are now float rather than decimal
  to acknowledge tha they are split adjusted.
- AdjustedClose is float because it is split and dividend adjusted.
