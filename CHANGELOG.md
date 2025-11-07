# Changelog

## [2.2.0] - 2025-11-06

### Added
- `GeoPoseLocalization.cs`: OSCP (Open Spatial Computing Platform) compliant GeoPose localizer. Uses the new `/geoposeoscp` [API endpoint](https://api.immersal.com/geoposeoscp).
  See: [Protocol specification](https://github.com/OpenArCloud/oscp-geopose-protocol) / [Read more](https://www.openarcloud.org/oscp)
- `GPSLocationProvider.cs`: native (double-precision) GPS tracking
- Core plugin: ENU->ECEF convertion functions
- REST: Support for upload cancellation token in `RequestUpload`

### Fixed
- RGB image memory issue
- Wrong `solverType` when it's not 0 or prior
- Android plugin: Support [16 KB page size requirement by Google](https://android-developers.googleblog.com/2025/05/prepare-play-apps-for-devices-with-16kb-page-size.html)
- iOS: `NativeLocation.mm` plugin now returns `ellipsoidalAltitude` as GPS altitude instead of MSL (mean sea level). This matches the behaviour on Android devices and Immersal's geo-related APIs.
- Minor bug fixes and SDK streamlining
- Compiler warnings

### Changed
- Updated all plugins to 2.2.0
- `Core.cs`: tidied up some function names and docs
- Note: The old 'proprietary' Immersal GeoPose localizer `GeoLocalization.cs` uses the `/geopose` endpoint. This may change in the future...

### Removed
- The even older endpoint `/geolocalize` hasn't been updated in years and has been removed from `REST.cs` etc.

## [2.1.1] - 2025-03-06

### Added
- MapManager.TryCreateMap can now apply map alignment at runtime
- Server/GeoLocalization now exposes upload Progress via a public UnityEvent
- ImmersalSDK now has a toggle for automatic initialization on Awake
- ImmersalSession has additional toggle for restarting after reset
- SceneDataProcessors can now be added/removed at runtime
- Custom editor for PoseFilter
- Custom editors for the provided localization method implementations
- More public properties for ImmersalSession

### Fixed
- PoseFilter bug causing unstable filtering
- CameraData not disposed when unused by the Localizer
- Prior localization using faulty poses
- Prior localization missing REST parameters
- TrackingAnalyzer tracking quality bug when dropping to 0
- Some TrackingAnalyzer events not firing correctly

## [2.1.0] - 2025-01-09

### Added
- Localization with prior pose with the new SolverType and related options
- Event and boolean for user validation completion at SDK initialization
- IPlatformConfiguration for setting options (eg. image format) once or per update
- Utility functions in ILocalizer to better support custom localization scripts
- CustomLocalizationSample scene and script

### Changed
- The 2.x.x SDK is no longer considered to be in a beta state
- Updated all plugins to 2.1.0
- The CameraData structure has been refactored into a new set of interfaces and classes
- ARFoundationSupport updated to work with refactored CameraData system

### Fixed
- ImmersalSession starting localization too early when adding maps at runtime
- MapManager removing embedded maps while localizing against them
- Small RealtimeMapping sample bugs

### Removed
- Old CameraData struct and related logic
- Support for platforms using old CameraData (all platform packages older than 2.1.0)

## [2.0.4] - 2024-11-20

### Added
- Screen orientation override methods in ARFoundationSupport
- Mechanism to support project validation issues from external packages

### Changed
- Default project issues now use the new issue provider mechanism

### Fixed
- Missing script references in a couple of prefabs

### Removed
- Hardcoded Magic Leap specific project issue

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
