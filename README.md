# Parental Control for Windows

> **Vibe Coded Disclaimer**
>
> This application was entirely vibe coded using **Claude Code with Claude Opus 4.6 (High)** and should be treated as such. The code was generated through conversational AI-assisted development — no line was hand-written. While functional and tested on the developer's machine, it has not undergone formal code review, security audit, or extensive QA. Use at your own discretion, review the source before deploying in any sensitive environment, and expect rough edges.

## Download

Download the latest installer (`ParentalControlSetup.exe`) from the [GitHub Releases page](https://github.com/robertpin/WindowsParentalControl/releases). Run it as administrator and follow the on-screen instructions.

---

## What Is This?

Parental Control is a **Windows desktop application** that lets administrators enforce screen time limits and usage schedules on local Windows user accounts. It consists of two components:

- **Admin UI** — a WPF desktop app where you configure per-user time limits and schedules, view usage, and monitor events.
- **Background Service** — a Windows Service that runs silently, tracks active sessions, enforces limits, and forcefully logs users off when they exceed their allowed time or fall outside their permitted schedule.

---

## Features

### User Management
- Automatically discovers all local Windows user accounts
- Filters out built-in system accounts (DefaultAccount, WDAGUtilityAccount, Guest)
- Distinguishes between standard users and administrators
- Built-in accounts and the Administrators group are identified by SID, so detection works on any Windows display language (e.g. "Администраторы" on Russian Windows)
- Administrator accounts are protected — they cannot be restricted (displayed as read-only in the UI), and the service never enforces limits on a member of the Administrators group, even if it is still marked as restricted in the database

### Daily Time Limits
- Set a maximum number of minutes per day for each user (default: 120 minutes)
- Usage is tracked in real time, accumulating every 60 seconds
- Administrators can directly edit a user's current daily usage from the Admin UI (useful for correcting usage)
- Extra time is granted as a bonus for today (+15 / +30 / +60 min buttons or a custom number of minutes) on top of the daily limit, so the usage shown stays the real time spent; the bonus applies to that day only
- A bonus of N minutes gives the user N minutes more than they had left. If the schedule window is what stops them (e.g. in the evening with allowance to spare), the window end for that day moves by N: at 20:30 with the window ending at 21:30, +30 min means until 22:00; after the end, it counts from now. A bonus granted when the allowance is what stops the user (e.g. in the afternoon) leaves the window alone; the extension never goes past 23:59
- Resets automatically at midnight

### Schedule Windows
- Define allowed hours for each user (e.g., 08:00 to 22:00)
- Times can be typed without a colon: `2200`, `22`, `730` or `22.00` are understood as `22:00`, `22:00`, `07:30` and `22:00`
- Users are locked out when their schedule window closes (see Lockout Instead of Logoff)
- Login attempts outside the schedule window are denied

### Per-Day Overrides
- Both the daily limit and the schedule window can be overridden for individual days of the week (e.g. more time and a later end on Friday and Saturday)
- Each field of an override is optional: an empty field uses the default value, so you can change only the end of the window and keep the default limit
- The dashboard shows the limit and schedule window in effect today

### Automatic Enforcement
- The background service checks all active sessions every 60 seconds
- If a user exceeds their daily limit (plus any bonus for today), their session is locked out
- If a user is logged in outside their allowed schedule, their session is locked out
- Login attempts are blocked if the user has already exhausted their daily limit or is outside their schedule

### Logoff Warnings
- A restricted user gets a message box 10 minutes and 1 minute before the session is locked, whichever comes first: the daily limit or the end of the schedule window
- Each warning is shown once per session; if extra time is granted, the warnings are shown again when the new deadline approaches
- No warnings in a locked or disconnected session; a pending warning is shown after the session resumes
- The message never blocks the service

### Lockout Instead of Logoff
- When time is up, the session is disconnected instead of logged off: the user sees the sign-in screen, while open documents and games stay as they are
- If a parent grants more time (bonus, schedule change), the user signs back in and continues where they left off
- Signing back in while still out of time shows a message and locks the session again a few seconds later
- If no more time is granted within 15 minutes, the session is logged off, so nothing keeps running in it (voice chat, music, downloads)
- A new login while out of time is still denied right away (nothing to lose yet), and clock tampering still logs the user off

### Telegram Bot (optional)
Check the status and grant time from your phone, also when you are away from home.
- Create a bot with [@BotFather](https://t.me/BotFather) (`/newbot`), then in the Admin UI open **Telegram**, paste the token and press **Save**
- Press **Generate pairing code** and send `/start <code>` to your bot within 10 minutes; repeat for each parent. The bot answers only paired chats; five wrong codes invalidate the current one
- `/status` shows, per restricted user: time used and remaining, today's allowed hours, the session state (active, screen locked, locked out), with buttons **+15 / +30 / +60 min**, **−15 / −30 / −60 min** and **Lock now** / **Unlock**
- **−N** leaves the child N minutes less than they really have left (never below zero; if less than N is left, time is up right away). The child gets a message either way
- When a session is locked out because time is up, you get a message with the same buttons; adding time lets the child continue where they left off
- **Lock now** locks the user out for the rest of the day (logged off after 15 minutes like any lockout); granting bonus time or **Unlock** lifts it
- The service only makes outgoing HTTPS requests to Telegram (long polling); nothing listens on the computer. The bot token is stored in the database, which restricted users cannot read
- Note: user names and usage are sent through Telegram's servers

### Event & Audit Logging
All enforcement actions are logged with timestamps, user SIDs, and details:

| Event Type | Description |
|---|---|
| `LOGIN` | User logged in successfully |
| `LOGOUT` | User logged out |
| `SLEEP` | System entered sleep mode |
| `WAKE` | System resumed from sleep |
| `LIMIT_REACHED` | Daily usage limit was reached |
| `FORCED_LOGOUT` | User was forcefully logged off |
| `LOGIN_DENIED` | Login attempt was rejected |
| `CLOCK_TAMPER` | System clock manipulation was detected |
| `SESSION_PAUSED` | Usage counting paused: session locked or disconnected |
| `SESSION_RESUMED` | Usage counting resumed: session unlocked or reconnected |
| `WARNING_SENT` | User was warned about an upcoming logoff |
| `BONUS_GRANTED` | Bonus minutes were granted or cleared for today |
| `LOCKED_OUT` | Time was up and the session was locked out, the user signed back in and was locked again, or the lockout was lifted |
| `TIME_TAKEN` | A parent took time away for today (Telegram) |
### Clock Tamper Detection
- Detects when the system clock is set backwards (e.g., a user trying to gain extra screen time)
- Immediately fills the restricted user's daily usage to their maximum allowed minutes, locking them out for the rest of the day
- Forces the user off the system
- Logs a `CLOCK_TAMPER` event with timestamps showing the time reversal

### Sleep/Wake Awareness
- Usage tracking pauses when the system enters sleep mode
- Resumes accurately on wake — sleep time is not counted against the user
- All active session times are flushed to the database before sleep
- Retroactively detects undetected sleep/hibernate events by identifying gaps in session activity greater than 2 minutes

### Locked and Disconnected Sessions
- Usage is not counted while a session is locked (Win+L, lock screen) or disconnected (switch user, RDP disconnect)
- Counting resumes when the session is unlocked or reconnected; partial minutes before the lock are kept
- The schedule window still applies: a locked or disconnected session is locked out and later logged off when the window closes
- Sessions that are already locked or disconnected when the service starts are detected as paused

### Data Retention
- Events and usage records older than 30 days are automatically deleted
- The event log is also capped at the newest 50,000 events, in case frequent logins or lock/unlock flood it
- Cleanup runs daily at midnight
- Keeps the database size manageable over time

### Security
- The `C:\ProgramData\ParentalControl\` directory is locked via ACLs to SYSTEM and Administrators only
- Restricted users cannot directly access or modify the database

### Light and Dark Theme
- The Admin UI follows the Windows app mode (Settings → Personalization → Colors): light or dark theme and title bar, switching live when the setting changes

### Service Status Monitoring
- The admin UI polls the Windows Service status every 5 seconds
- Displays a green indicator when the service is running, red when it's down

---

## Architecture

```
+---------------------------+       +---------------------------+
|   ParentalControl.Admin   |       | ParentalControl.Service   |
|   (WPF Desktop App)       |       | (Windows Service)         |
|                           |       |                           |
|  - Dashboard View         |       |  - Session Tracker        |
|  - User Detail View       |       |  - Usage Monitor Worker   |
|  - Service Status Monitor |       |  - Session Change Handler |
+------------+--------------+       +------------+--------------+
             |                                   |
             |         Shared Library            |
             +--------->  ParentalControl.Core <-+
                        |                      |
                        |  - Data Models       |
                        |  - Repositories      |
                        |  - Database Manager  |
                        |  - Session Manager   |
                        |  - Native Methods    |
                        +----------+-----------+
                                   |
                            SQLite Database
                   (C:\ProgramData\ParentalControl\data.db)
```

Both the Admin UI and the Service share the same SQLite database. When you change a user's limits in the Admin UI, the Service picks up those changes on its next 60-second tick.

---

## Tech Stack

| Component | Technology |
|---|---|
| UI Framework | WPF (Windows Presentation Foundation) |
| UI Theme | Material Design Themes for WPF (Indigo/Amber, light or dark following Windows) |
| Architecture Pattern | MVVM (CommunityToolkit.Mvvm) |
| Backend Service | .NET Worker Service (Windows Service) |
| Database | SQLite (Microsoft.Data.Sqlite) |
| Logging | Serilog (rolling file, 30-day retention) |
| Platform Integration | Windows Terminal Services API (P/Invoke) |
| Installer | Inno Setup 6 |
| Target Framework | .NET 8.0 (framework-dependent: needs the .NET 8 Desktop Runtime, installed by the installer if missing) |
| Target OS | Windows (x64 only) |

---

## Project Structure

```
ParentalControl/
├── src/
│   ├── ParentalControl.Admin/           # WPF Admin UI
│   │   ├── App.xaml(.cs)                # App startup, Material Design theme config
│   │   ├── MainWindow.xaml(.cs)         # Main window with service status indicator
│   │   ├── Views/
│   │   │   ├── DashboardView.xaml(.cs)  # User list, admin list, recent events
│   │   │   └── UserDetailView.xaml(.cs) # Per-user limits config and event history
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs         # Service polling, view navigation
│   │   │   ├── DashboardViewModel.cs    # User discovery, event loading
│   │   │   ├── UserDetailViewModel.cs   # Limit editing, validation, save/remove
│   │   │   ├── DayOverrideRow.cs        # One weekday row of the per-day overrides
│   │   │   └── UserRow.cs              # Per-user presentation model
│   │   ├── Helpers/
│   │   │   └── DataGridScrollHelper.cs  # Shared DataGrid scroll behavior
│   │   └── Services/
│   │       └── UserDiscovery.cs         # Windows local user enumeration
│   │
│   ├── ParentalControl.Core/            # Shared library
│   │   ├── Data/
│   │   │   ├── DatabaseManager.cs       # SQLite initialization and schema
│   │   │   ├── UserRepository.cs        # User CRUD
│   │   │   ├── LimitRepository.cs       # Limit config CRUD
│   │   │   ├── UsageRepository.cs       # Daily usage tracking
│   │   │   └── EventRepository.cs       # Event logging and queries
│   │   ├── Models/
│   │   │   ├── User.cs                  # User model (Id, Sid, Username, IsRestricted)
│   │   │   ├── LimitConfig.cs           # Limit model (DailyMinutes, ScheduleStart/End)
│   │   │   ├── LimitOverride.cs         # Per-weekday override of the limits
│   │   │   ├── UsageRecord.cs           # Usage model (UserId, Date, MinutesUsed)
│   │   │   ├── EventRecord.cs           # Event model (Timestamp, UserSid, EventType)
│   │   │   └── EventType.cs             # Event type enum
│   │   ├── Platform/
│   │   │   ├── SessionManager.cs        # WTS API wrapper (sessions, force logoff)
│   │   │   └── NativeMethods.cs         # P/Invoke for wtsapi32.dll
│   │   └── Logging/
│   │       └── LoggingConfig.cs         # Serilog configuration
│   │
│   └── ParentalControl.Service/         # Windows Service
│       ├── Program.cs                   # Host setup, DI registration
│       ├── ParentalControlServiceLifetime.cs  # Session change & power event handlers
│       ├── SessionTracker.cs            # Active session tracking & enforcement
│       └── UsageMonitorWorker.cs        # 60-second enforcement loop
│
├── tests/
│   └── ParentalControl.Tests/           # xUnit tests (effective limits, logoff warnings, lockout, time input, Telegram bot)
│
├── installer/
│   ├── ParentalControl.iss              # Inno Setup installer script
│   └── Output/                          # Generated installer output
│
├── build.ps1                            # Build & package script
├── icon.ico                             # Application icon
├── ParentalControl.slnx                 # Solution file
└── .gitignore
```

---

## Getting Started

### Prerequisites

- **Windows 10/11** (x64)
- **.NET 8 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Inno Setup 6** (for building the installer) — [Download](https://jrsoftware.org/isdownload.php)

### Clone the Repository

```bash
git clone https://github.com/your-username/ParentalControl.git
cd ParentalControl
```

### Running in Development

To run the Admin UI directly during development:

```bash
dotnet run --project src/ParentalControl.Admin
```

To run the Service in console mode during development:

```bash
dotnet run --project src/ParentalControl.Service
```

> **Note:** The service needs to run with administrator privileges to track sessions and enforce logoffs. Right-click your terminal and "Run as Administrator" before starting the service.

### Running the Tests

```bash
dotnet test tests/ParentalControl.Tests
```

### Building the Installer

The `build.ps1` script publishes both projects as framework-dependent win-x64 binaries (about 20 MB in total; the .NET runtime is not copied into them) and then invokes Inno Setup to create the installer:

```powershell
.\build.ps1
```

If the script fails with this error:

```
.\build.ps1 : File C:\Users\Robert\Desktop\ParentalControl\build.ps1 cannot be loaded because running scripts is
disabled on this system. For more information, see about_Execution_Policies at
https:/go.microsoft.com/fwlink/?LinkID=135170.
```

Run it with the execution policy bypassed:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\build.ps1
```

The build produces:
- `publish/service/` — Service binaries (framework-dependent)
- `publish/admin/` — Admin UI binaries (framework-dependent)
- `installer/Output/ParentalControlSetup.exe` — The installer

### Installing

Run `ParentalControlSetup.exe` as administrator. The installer will:

1. Install the .NET 8 Desktop Runtime if it is missing (downloaded from Microsoft, installed silently; it is shared with other .NET apps and updated by Windows Update)
2. Install files to `C:\Program Files\ParentalControl\`
3. Register and start the `ParentalControl.Service` Windows Service (auto-start, runs as LocalSystem)
4. Configure automatic service restart on failure (10s, 30s, 60s intervals)
5. Create Start Menu and Desktop shortcuts for the Admin UI — only for the administrator running the installer, so restricted users do not see them

### Uninstalling

Navigate to `C:\Program Files\ParentalControl\` and run `uninst000.exe`. This will launch the uninstaller, which will:

- Stop and remove the `ParentalControl.Service` Windows Service
- Remove all program files from `C:\Program Files\ParentalControl\`
- Remove the database and logs from `C:\ProgramData\ParentalControl\`
- Remove Start Menu and Desktop shortcuts

---

## How It Works

### Service Lifecycle

1. **Startup** — The service initializes the SQLite database, recovers any existing active sessions, and begins the 60-second monitoring loop.
2. **Session Change** — When a user logs on, the service checks their restrictions. If the user is outside their schedule or has exhausted their daily limit, login is denied (forced logoff). Otherwise, the session is tracked.
3. **Monitoring Loop** — Every 60 seconds, the service iterates over all active sessions. For each restricted user, it increments their daily usage and checks limits. Violations lock the session out; it is logged off if no more time is granted within 15 minutes.
4. **Logoff** — When a user logs off (or is forced off), accumulated session time is flushed to the database.
5. **Sleep/Wake** — On system sleep, all session times are flushed and tracking pauses. On wake, tracking resumes without counting sleep time.

### Data Flow

The Admin UI and the Service both read/write the same SQLite database at `C:\ProgramData\ParentalControl\data.db`. There is no API or IPC between them — the database is the shared state. SQLite's WAL (Write-Ahead Logging) mode ensures safe concurrent access.

### Database Schema

| Table | Purpose |
|---|---|
| `users` | Local Windows users (SID, username, restricted flag) |
| `limits` | Per-user limit config (daily minutes, schedule start/end) |
| `limit_overrides` | Per-user, per-weekday overrides of `limits`; `NULL` fields use the default |
| `usage` | Daily usage records (user, date, minutes used, bonus minutes granted for the day, locked by parent, schedule window end extended by a bonus) |
| `events` | Audit log of all enforcement events |
| `settings` | Application settings as key/value pairs (Telegram bot token, paired chats) |

---

## File Locations (After Installation)

| Path | Contents |
|---|---|
| `C:\Program Files\ParentalControl\admin\` | Admin UI binaries |
| `C:\Program Files\ParentalControl\service\` | Service binaries |
| `C:\ProgramData\ParentalControl\data.db` | SQLite database |
| `C:\ProgramData\ParentalControl\logs\` | Rolling log files (30-day retention) |

---

## License

This project is licensed under the [MIT License](LICENSE).
