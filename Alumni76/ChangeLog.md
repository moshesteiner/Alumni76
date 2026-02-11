# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

# DO NOT FORGET TO UPDATE THE DATE

## [2.1.2] - 2025-12-17
### Added
- HistoryLog page (Version history) for Admins only, in Reports section. Use ChangeLog.md file
- Users page - sort by lastLogin

### Fixed
- Minor bugs with SpecialAdmin in _layout.cshtml_

### Changed
- Adding 'Changed' and 'Remove' section support in History Page

## [2.1.1] - 2025-12-13
### Fixed
- Users page, fix Sort by Role
- Resolve Page - fix filter ActiveSenior by SubjectId
- Index Page - added '!' to SpecialAdmin password
- Index page add success/error messages
- Answer Page - fix filter Available Exams by allowedExams
- VerifyCode Page - logout after verification to login to the correct sybject
- AddUser Page - remove the password field. Generate random password
- AddUser Page - fix a bug when trying to add an existing user
- AddUser Page - add spinner when adding a user.  Still need a spinner when adding a bulk....
- EmailService & NewUserWelcome template - add SubjectTitle to New User Email

### Added
- Subject Model - add Active column
- Index Page - filter our non active Subjects --> Block entire subject

## [2.1.0] - 2025-12-12
### Added
- **New Feature:** Implemented the Azure Storage Cleanup Utility in the Admin section to identify orphaned files. (See: `StorageCleanup.cshtml.cs`)
- Added `IStorageService` methods: `ListAllBlobNamesAsync` and `DeleteBlobsAsync` for cleanup functionality.
- Custom confirmation dialog logic added via `alert-override.js` for irreversible actions like file deletion.

### Changed
- Refactored `AzureBlobStorageService` to retrieve the active container name dynamically from `StorageOptions`.
- Updated `Program.cs` to correctly incorporate the `gitHash` and automated build number into the `AppSettings`.

### Fixed
- Corrected a bug where the `FilePathOrUrl` normalization was not consistently extracting the relative blob name from the full Azure URL.
- Resolved an issue in `AddIssue.cshtml.cs` and `Report.cshtml.cs` where the container name was hardcoded for file uploads.

## [2.0.0] - 2025-10-01
### Added
- Support for multi Subjects in the database and for each User
- Initial support for Azure Blob Storage as the primary file storage service.
- Implemented `IStorageService` interface to allow for future storage provider swapping.

### Removed
- Deprecated and removed all code references to Google Cloud Storage (GCS) and the related `StorageOptions`.

### Security
- Strengthened role-based authorization for all `/Admin` pages, specifically for the new Storage Cleanup utility.