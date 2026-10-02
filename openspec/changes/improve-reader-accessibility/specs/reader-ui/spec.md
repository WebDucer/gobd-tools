# Spec Delta: reader-ui

## MODIFIED Requirements

### Requirement: The reader adapts to system theme variant
A reader must stay legible wherever it is used, and some people cannot read it at ordinary
contrast at all. The system SHALL offer System, Light, Dark, High contrast (dark) and High contrast
(light) appearances. By default it SHALL follow the platform's light or dark preference and its high
contrast setting. Every text, link, status marking and focus indicator SHALL meet WCAG AA contrast in
every variant.

#### Scenario: Automatic theme matching
- **WHEN** the reader runs with the System appearance setting on an operating system configured for
  dark mode or light mode, without high contrast
- **THEN** the reader SHALL present its window, navigator, tabs, grids and dialogs using that theme
  variant

#### Scenario: System follows the platform's high contrast setting
- **WHEN** the reader runs with the System appearance setting and the operating system reports that
  high contrast is on
- **THEN** the reader SHALL present itself in high contrast, dark or light as the operating system
  is, and SHALL change again when that setting changes, without being restarted

#### Scenario: A platform that does not report high contrast
- **WHEN** the reader runs on a platform that does not report a high contrast setting
- **THEN** System SHALL follow the light or dark preference alone, and both high contrast
  appearances SHALL remain available to choose explicitly

#### Scenario: Explicit theme override
- **WHEN** a person selects Light, Dark, High contrast (dark) or High contrast (light) in Settings
- **THEN** the reader SHALL immediately apply that appearance regardless of the operating system's
  setting

#### Scenario: Theme legibility for findings and links
- **WHEN** the reader displays conformant status, non-conformant findings, followable foreign keys,
  values that refer to nothing, or secondary text, in any appearance
- **THEN** that text SHALL contrast with its background by at least 4.5:1, or 3:1 where it is large
  text

#### Scenario: Values in the selected record stay legible
- **WHEN** a record is selected and its row is drawn in the selection colour
- **THEN** its followable values and its values that refer to nothing SHALL still meet the same
  contrast against the selection colour, in every appearance

#### Scenario: Status is not told by colour alone
- **WHEN** the reader marks a value that can be followed, a value that refers to nothing, or the
  export's verdict
- **THEN** the marking SHALL also be told apart without colour, by underline, strikethrough or
  wording, so that it survives high contrast and colour blindness

### Requirement: User can configure and persist preferences
Preferences belong to the person running the application and must not be lost when the application
closes. The system SHALL provide a Settings window accessible from the application menus and keyboard
shortcuts, SHALL allow selecting the appearance theme, display language and zoom level, and SHALL
persist these choices together with the navigator's width and whether it is collapsed, in the user's
platform configuration directory, without writing to the export.

#### Scenario: Opening settings from the menu or shortcut
- **WHEN** a person chooses Settings from the menu or presses the standard shortcut (Cmd+, on macOS, Ctrl+, on other platforms)
- **THEN** the system SHALL present the Settings window, or bring it forward if already open

#### Scenario: Preferences persist across launches
- **WHEN** a person changes the theme, language or zoom level, resizes the navigator or collapses
  it, and subsequently restarts the reader
- **THEN** the reader SHALL start with the previously chosen theme, language, zoom level, navigator
  width and navigator state

#### Scenario: Absent or unreadable configuration falls back safely
- **WHEN** no preferences file exists or the file is unreadable
- **THEN** the reader SHALL start with the System theme, the operating system's detected language,
  actual size and an expanded navigator of its default width, without reporting an error

#### Scenario: A setting this reader does not know
- **WHEN** the preferences file holds a value this version of the reader does not know for one
  setting, such as an appearance added by a later version
- **THEN** that setting SHALL take its default and every other setting in the file SHALL be kept

### Requirement: Interactive controls provide baseline accessibility
Someone using assistive technology knows a control only by the name it exposes, and someone using
the keyboard knows where they are only by the focus they can see. The system SHALL give every
interactive control and every input an accessible name in the display language, and SHALL show a
clearly visible focus indicator on whichever control has keyboard focus.

#### Scenario: Accessible names for controls
- **WHEN** assistive technology inspects interactive elements such as table tabs, close buttons,
  filter and sort chips, action buttons, the inputs and pickers of the filter, sort and figure
  editors, the navigator's splitter and its show control, or the reading progress
- **THEN** each element SHALL expose an accessible name describing its action or content

#### Scenario: Names follow the display language
- **WHEN** the display language is changed
- **THEN** every accessible name SHALL be given in the new language

#### Scenario: Visible keyboard focus
- **WHEN** a person navigates interactive controls using keyboard input
- **THEN** the currently focused control SHALL display a clear, distinct visual focus indicator,
  including a button that shows only a symbol, such as a tab's or a chip's close button

#### Scenario: Focus indicators are visible in every appearance
- **WHEN** a focus indicator is drawn in any appearance
- **THEN** it SHALL contrast with its surroundings by at least 3:1

## ADDED Requirements

### Requirement: The interface can be enlarged
A person who needs larger text needs all of it larger, including the navigator, and must not lose
anything by enlarging it. The system SHALL let a person zoom the whole interface, including its
dialogs, its editors and the record actions, from actual size to twice that size, and SHALL keep
every control and every value reachable at any zoom level.

#### Scenario: Zooming from the keyboard
- **WHEN** a person presses the command key with plus, with minus, or with zero (the command key
  being Cmd on macOS and Ctrl elsewhere)
- **THEN** the reader SHALL zoom in by one step, zoom out by one step, or return to actual size,
  within the range from 100 % to 200 %

#### Scenario: Zooming from Settings and the menu
- **WHEN** a person chooses a zoom level in Settings, or Zoom In, Zoom Out or Actual Size in the
  View menu
- **THEN** the reader SHALL apply it at once

#### Scenario: Everything is enlarged together
- **WHEN** a zoom level other than actual size is in force
- **THEN** the navigator, the tabs, the table toolbar, the records, the in-window menu bar, the
  Settings, About and text windows, the filter, sort and figure editors, and the record actions
  SHALL all be presented at that zoom level

#### Scenario: Nothing is lost at the largest zoom
- **WHEN** the interface is zoomed to 200 %
- **THEN** every control and every value SHALL remain reachable, by scrolling where it does not fit

### Requirement: The navigator can be resized and collapsed
Long table names and large text need a wider navigator, while a wide table at a large zoom level
needs all the room it can get. The system SHALL let a person change the navigator's width and
collapse it to a narrow strip, with the mouse and with the keyboard, and SHALL NOT do either on its
own.

#### Scenario: Resizing the navigator
- **WHEN** a person drags the boundary between the navigator and the tables, or focuses that
  boundary and presses the left or right arrow key
- **THEN** the navigator SHALL become narrower or wider, within a minimum width and a maximum of half
  the window

#### Scenario: Collapsing and restoring the navigator
- **WHEN** a person chooses Show Navigator in the View menu, presses Ctrl+B (Ctrl+Cmd+S on macOS), or
  activates the control on the collapsed strip
- **THEN** the navigator SHALL collapse to a narrow strip that still offers a control to restore it,
  or be restored at the width it had

#### Scenario: Collapsing the navigator while it has focus
- **WHEN** the navigator is collapsed while keyboard focus is inside it
- **THEN** focus SHALL move to the tab in front, rather than be lost

#### Scenario: A collapsed navigator stays current
- **WHEN** the table in view changes while the navigator is collapsed, and the navigator is then
  restored
- **THEN** it SHALL indicate the table in view

#### Scenario: Zooming does not collapse the navigator
- **WHEN** a person changes the zoom level
- **THEN** the navigator SHALL keep its state, so that only the person decides whether it is shown

### Requirement: Moving through the navigator does not open tables
Moving through a list from the keyboard is how a person reads it, and every move cannot also be
taken as a choice. The system SHALL let the arrow keys move through the navigator without opening
anything, and SHALL open a table only when the person asks for it.

#### Scenario: Arrow keys only move
- **WHEN** a person moves through the navigator's entries with the arrow keys
- **THEN** no table SHALL be opened and the tab in front SHALL NOT change

#### Scenario: Opening a table from the keyboard
- **WHEN** a person presses Enter on a table's entry in the navigator
- **THEN** the system SHALL open that table, bring its tab to the front and move focus to its
  records

#### Scenario: Opening a table with the mouse
- **WHEN** a person clicks a table's entry in the navigator
- **THEN** the system SHALL open that table and bring its tab to the front

### Requirement: Every command can be reached from the keyboard
A reader that needs a mouse is closed to people who cannot use one, and slow for people who would
rather not. The system SHALL offer every command in a menu and from the keyboard. Commands used
often SHALL have shortcuts that follow each platform's conventions. A shortcut SHALL mean one
thing in any one place.

#### Scenario: Shortcuts for the workspace
- **WHEN** a person presses one of these shortcuts, where the command key is Cmd on macOS and Ctrl
  elsewhere: command+O, command+Shift+O, command+comma, command+W (also Ctrl+F4 on Windows and
  Linux), Ctrl+Tab, Ctrl+Shift+Tab (also Cmd+Shift+] and Cmd+Shift+[ on macOS), command+1 to
  command+9, F6, Shift+F6, or F1
- **THEN** the reader SHALL in that order open an archive, open a folder, show Settings, close the
  table tab in front, bring the next or previous tab to the front, bring the tab in that place to the
  front with the summary as the first, move focus to the next or previous area, or show the shortcut
  overview

#### Scenario: Shortcuts for the table in front
- **WHEN** a table is in front and a person presses command+F, F3 or Shift+F3 (Cmd+G or Cmd+Shift+G
  on macOS), Alt+Left or Alt+Right (Cmd+[ or Cmd+] on macOS), or Ctrl+G (Cmd+L on macOS)
- **THEN** the reader SHALL in that order open the filter editor, move to the next or previous
  referring record while referring records are being stepped through, go back or forward, or ask for
  a record number to go to

#### Scenario: Closing a tab from the keyboard
- **WHEN** a person presses the shortcut for closing a tab
- **THEN** the table tab in front SHALL close and focus SHALL move into the tab that comes to the
  front; with the summary in front nothing SHALL close, and the window SHALL stay open

#### Scenario: Next and previous tab wrap around
- **WHEN** a person moves to the next tab from the last one, or to the previous tab from the summary
- **THEN** the summary, or the last tab, SHALL come to the front

#### Scenario: Moving between areas
- **WHEN** a person presses F6 or Shift+F6
- **THEN** focus SHALL move to the next or previous of the navigator, the table's toolbar and the
  table's records (or the summary), skipping the navigator while it is collapsed

#### Scenario: Shortcuts are shown where commands are found
- **WHEN** a person opens a menu, or the shortcut overview
- **THEN** each command SHALL show its shortcut for the platform the reader runs on, and the overview
  SHALL list every shortcut in the display language

#### Scenario: Menus can be opened from the keyboard
- **WHEN** the reader runs on Windows or Linux and a person presses Alt with the letter a menu's
  title marks
- **THEN** that menu SHALL open, and the marked letters SHALL follow the display language

#### Scenario: A text field keeps its own keys
- **WHEN** keyboard focus is in a text field and a person presses a key that the field uses itself,
  such as copy, Enter or the arrow keys
- **THEN** the key SHALL act on the field and SHALL NOT run a command of the reader

#### Scenario: Editors and dialogs are operated from the keyboard
- **WHEN** a filter, sort or figure editor, or a dialog, is opened
- **THEN** focus SHALL move into it, Enter SHALL apply an editor, Escape SHALL close it and return
  focus to where it was opened from, and Cmd+W SHALL close a dialog on macOS

### Requirement: A record's references can be followed from the keyboard
Following a reference is the reader's central act, and it must not depend on aiming a pointer at a
cell. The system SHALL offer, for the record that has focus, every reference it can follow and
every table that refers to it, from the keyboard and from the context menu alike.

#### Scenario: Opening the record's actions
- **WHEN** a record has focus and a person presses Enter, Shift+F10 or the Menu key, or right-clicks
  a record
- **THEN** the system SHALL present the record's actions next to that record: each foreign key it
  can follow, each table whose records refer to it, and copying the record

#### Scenario: Where a value leads is said before it is followed
- **WHEN** the record's actions are presented
- **THEN** each foreign key SHALL be named by its columns and by the table it leads to, and one whose
  value refers to nothing SHALL say so, before anything has been followed

#### Scenario: Following from the record's actions
- **WHEN** a person chooses a foreign key or a referring table among the record's actions
- **THEN** the system SHALL navigate exactly as it does when the reference is followed with the
  mouse, or the referring records are asked for

#### Scenario: Clicking a value still follows it
- **WHEN** a person clicks a value that takes part in a foreign key
- **THEN** the system SHALL follow that key directly, as before

### Requirement: Focus follows navigation
Someone who follows a reference from the keyboard has to arrive where the navigation arrived, or
they are left reading something else. The system SHALL move keyboard focus to the record a
navigation reaches.

#### Scenario: Arriving at a record
- **WHEN** a navigation positions a table at a record, whether by following a reference, stepping
  between referring records, going back or forward, or going to a record number
- **THEN** that record SHALL have keyboard focus

#### Scenario: Returning to a tab
- **WHEN** a person brings a table's tab to the front from the keyboard
- **THEN** focus SHALL move to the record that table is positioned at

### Requirement: Back and Forward retrace navigations
Following references leads from table to table, and finding the way back by hunting through tabs
undoes the convenience of following them. The system SHALL remember where each navigation started,
and SHALL let a person go back to it and forward again, as a browser does.

#### Scenario: Going back
- **WHEN** a person has followed a reference, asked for the records referring to one, or gone to a
  record number, and then chooses Back
- **THEN** the system SHALL present the table that navigation started from, positioned at the record
  it started from

#### Scenario: Going forward
- **WHEN** a person has gone back and then chooses Forward
- **THEN** the system SHALL present again the table and record that navigation reached

#### Scenario: A new navigation after going back
- **WHEN** a person goes back and then navigates anew
- **THEN** the navigations that Forward would have retraced SHALL be discarded

#### Scenario: What is not a navigation
- **WHEN** a person chooses a table in the navigator, switches tabs, or steps between referring
  records
- **THEN** no new place SHALL be remembered, and Back from a walk through referring records SHALL
  return to the record the walk started from

#### Scenario: Going back to a table whose tab was closed
- **WHEN** Back or Forward leads to a table whose tab has since been closed
- **THEN** the system SHALL open that table again and position it at the remembered record

#### Scenario: Going back into a view that hides the record
- **WHEN** Back or Forward leads to a record that the filter of the target table's view hides
- **THEN** the system SHALL treat it as any navigation into such a view: remove the filter, keep the
  sort, and say so with the table

#### Scenario: Nothing to go back to
- **WHEN** there is no earlier or later navigation
- **THEN** Back or Forward SHALL be unavailable, and the menu SHALL show it so

#### Scenario: A new export starts a new history
- **WHEN** another export is opened
- **THEN** the navigations remembered for the previous one SHALL be forgotten

### Requirement: A record can be reached by its number
Findings, notes and colleagues cite a record by the number the file gives it. The system SHALL let
a person go to a record of the table in front by that number.

#### Scenario: Going to a record
- **WHEN** a person asks to go to a record and enters a record number the table holds
- **THEN** the system SHALL position the table at that record, with that record indicated and
  focused

#### Scenario: A number the table does not hold
- **WHEN** the number entered is not a record number of that table
- **THEN** the system SHALL say so where the number was entered, and SHALL leave the table where it
  was

#### Scenario: A record hidden by the view
- **WHEN** the record is hidden by the filter of the table's view
- **THEN** the system SHALL remove the filter, keep the sort, and say so with the table, as for any
  navigation

#### Scenario: Only where there are records
- **WHEN** the summary is in front, or the table in front has no data to show
- **THEN** going to a record SHALL be unavailable

### Requirement: A record can be copied
Someone who reads an export has to quote what they read, and retyping a value invites the very
error the reader exists to avoid. The system SHALL let a person copy the record that has focus, as
shown, in a form that pastes into a spreadsheet.

#### Scenario: Copying a record
- **WHEN** a record has focus and a person presses the command key with C, or chooses Copy Record
  among the record's actions
- **THEN** the clipboard SHALL hold two lines, separated by tabs: the record number and the declared
  column names, then the record's number and its values exactly as shown

#### Scenario: Values that would break the lines
- **WHEN** a copied value contains a tab, a line break or a quotation mark
- **THEN** that value SHALL be enclosed in quotation marks with its own quotation marks doubled, so
  that it pastes as one cell

### Requirement: Wide tables can be scrolled from the keyboard
A table may declare fifty columns, and what lies beyond the window's edge is as much part of the
record as what is in view. The system SHALL let a person scroll a table's records sideways from the
keyboard.

#### Scenario: Scrolling sideways
- **WHEN** a table's records have focus and a person presses the left or right arrow key
- **THEN** the records SHALL scroll sideways, leaving the record that has focus unchanged

### Requirement: Records are identified to assistive technology
A row that announces nothing, or the name of a type, tells a screen reader user nothing about the
record. The system SHALL give every presented record an accessible name made of its record number
and its values under their column names. It SHALL NOT be required to expose records as a table
with row and column headers.

#### Scenario: A record is announced
- **WHEN** assistive technology inspects a presented record
- **THEN** its name SHALL state its record number as the file counts records, followed by each
  declared column's name and value as shown

#### Scenario: A value that refers to nothing is announced
- **WHEN** a record holds a foreign key value that matches no record of the referenced table
- **THEN** the record's name SHALL say so at that value, as the presentation marks it

### Requirement: Status messages are announced
What the reader says about its progress and about a navigation is shown where it happened, which is
not where a screen reader is listening. The system SHALL announce its status messages to assistive
technology without moving focus.

#### Scenario: Messages that are announced
- **WHEN** the reader reports reading progress, the outcome of a navigation, that a filter was
  removed, the position while stepping between referring records, that a table is not ready yet, or
  an editor's refusal of what was entered
- **THEN** that message SHALL be announced to assistive technology, and focus SHALL stay where it was

#### Scenario: Progress is not announced at every step
- **WHEN** reading progresses continuously
- **THEN** the progress SHALL be announced at intervals and when a table finishes, rather than at
  every update the display receives

### Requirement: The reader lists its keyboard shortcuts
Shortcuts that cannot be found are shortcuts nobody uses. The system SHALL present every shortcut
it offers, for the platform it runs on and in the display language, from its Help menu and from F1.

#### Scenario: Showing the shortcut overview
- **WHEN** a person presses F1 or chooses Keyboard Shortcuts in the Help menu
- **THEN** the reader SHALL show every command that has a shortcut, with that shortcut as the
  platform writes it, grouped by where it applies

#### Scenario: Closing the shortcut overview
- **WHEN** the shortcut overview has focus and a person presses Escape
- **THEN** it SHALL close and focus SHALL return to where it was
