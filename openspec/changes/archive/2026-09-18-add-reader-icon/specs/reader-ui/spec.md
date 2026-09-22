## ADDED Requirements

### Requirement: The reader shows its icon wherever an application's icon is shown
Someone with several windows open, or looking for the reader among their applications and
downloads, recognises it by its picture before its name, and a generic icon gives them nothing to
recognise. The system SHALL show the reader's icon wherever the platform shows an application's
icon, and SHALL keep that icon recognisable on a light and on a dark background.

#### Scenario: The Windows file shows the icon before it is started
- **WHEN** a person looks at the downloaded Windows executable in the file manager or on the
  desktop
- **THEN** it SHALL show the reader's icon

#### Scenario: The running window shows the icon
- **WHEN** the reader runs on Windows, or on a Linux desktop that shows the icons of windows
- **THEN** its window SHALL show the reader's icon in the title bar and in the taskbar or window
  switcher

#### Scenario: The macOS application shows the icon
- **WHEN** a person sees the macOS application in the disk image, in Finder or in the Dock
- **THEN** it SHALL show the reader's icon, and macOS SHALL show that icon as delivered rather than
  inside a frame of its own

#### Scenario: The icon is recognisable on a dark background
- **WHEN** the icon is shown on a dark background, such as a dark taskbar or a dark Dock
- **THEN** the whole drawing SHALL remain visible, including its dark parts
