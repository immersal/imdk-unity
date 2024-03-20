# Changelog

## [2.0.1] - 2024-03-20

### Added
- Real time mapping sample and related scripts
- Confidence value in REST SDKLocalizeResult and ServerLocalization
- New events in Localizer and MapManager
- Localization method option in map download sample
- ImmersalSession burst mode parameters

### Changed
- Plugins updated to 2.0.1
- Exposed more ARFoundationSupport methods as public
- ARFoundationSupport initialization logic moved to ConfigurePlatform
- ServerLocalization now always includes rotation in requests
- IImmersalSession.StopSession() now has a parameter for cancelling the currently running task.

### Fixed
- Project validation bugs
- Localizer & LocalizationMethod runtime configuration bugs
- Missing mapId from ServerLocalization LocalizeInfo
- XRMap Metadata parsing bug
- RestartSdk bugs

### Removed
- Legacy embedded map logic in map download sample

## [2.0.0] - 2024-02-12

The SDK has been refactored in a major way.

> **There is no backwards compatibility with older SDK versions.**

### Added

- Interfaced component architecture
- Asynchronous localization with multiple methods
- Multiple XRSpace support
- Customizable data processing chains
- Map specific localization options
- Streamlined map configuration
- Improved events
- Updated Editor scripts for better UX
- Updated REST api framework
- Automated project validation
- MapManager for runtime map management
- ImmersalLogger as a debugging utility
- Unity package format

### Removed

- Old inheritance based architecture
- Map loading during edit mode
- ARHelper and other deprecated classes