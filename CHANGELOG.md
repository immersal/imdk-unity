# Changelog

## [2.0.3] - 2024-09-11

### Added
- More options for PoseSmoother
- Experimental options for PoseFilter
- IDataProcessingChain interface

### Changed
- Plugins updated to 2.0.3
- Reduced map loading time and memory usage
- Improved realtime mapping speed
- DataProcessingChain type argument constrained to be a reference type
- XRSpace now uses the new SceneUpdateData from SceneUpdater in processing

### Fixed
- PoseFilter issues with large maps / offsets
- Custom server URL serialization bug
- ImmersalSession LocalizeOnce() checks current task before awaiting it

## [2.0.2] - 2024-04-26

### Added
- Custom server option in ImmersalSDK object
- Dense as optional parameter in map construction REST API (defaults to true)

### Changed
- Plugins updated to 2.0.2

### Fixed
- Project validation not recognizing old format XR Plugin names
- Server selection in ImmersalSDK object not working as expected
- XRMaps not working when ISceneUpdateable is not a direct parent
- Unity stuck on "domain reloading" on Windows due to plugin issues

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
