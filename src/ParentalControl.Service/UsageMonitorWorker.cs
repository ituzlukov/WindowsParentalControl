using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;
using ParentalControl.Core.Platform;
namespace ParentalControl.Service;

public sealed class UsageMonitorWorker : BackgroundService
{
    private readonly SessionTracker _sessionTracker;
    private readonly Serilog.ILogger _logger;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RelockDelay = TimeSpan.FromSeconds(5);
    private DateOnly _lastCleanupDate = DateOnly.MinValue;
    // Ticks and session-resume handling may enforce the same session concurrently
    private readonly object _enforceSync = new();
    private readonly ConcurrentDictionary<int, DateTime> _relockPending = new();

    public UsageMonitorWorker(SessionTracker sessionTracker, Serilog.ILogger logger)
    {
        _sessionTracker = sessionTracker;
        _logger = logger;
        _sessionTracker.SessionResumed += OnSessionResumed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Usage monitor worker started");

        try
        {
            ProcessTick();
            RunCleanupIfNeeded();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during initial usage monitor tick");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TickInterval, stoppingToken);

            if (!_sessionTracker.IsAwake)
                continue;

            try
            {
                ProcessTick();
                RunCleanupIfNeeded();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during usage monitor tick");
            }
        }
    }

    private void ProcessTick()
    {
        _sessionTracker.TickAllSessions();

        foreach (var sessionId in _sessionTracker.ActiveSessions.Keys)
        {
            EnforceSession(sessionId);
        }
    }

    private void OnSessionResumed(int sessionId)
    {
        // Off the session change thread: enforcement touches the database and WTS APIs
        Task.Run(() =>
        {
            try
            {
                EnforceSession(sessionId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error enforcing limits on resume of session {SessionId}", sessionId);
            }
        });
    }

    /// <summary>
    /// Applies the daily limit (plus bonus) and the schedule window to one session; see <see cref="LockoutPolicy"/>.
    /// Called every tick and right after a session is unlocked or reconnected.
    /// </summary>
    private void EnforceSession(int sessionId)
    {
        lock (_enforceSync)
        {
            if (!_sessionTracker.ActiveSessions.TryGetValue(sessionId, out var session)) return;

            var user = UserRepository.GetBySid(session.UserSid);
            var dateTimeNow = DateTime.Now;
            var today = DateOnly.FromDateTime(dateTimeNow);
            var now = TimeOnly.FromDateTime(dateTimeNow);

            var limit = user is null || !user.IsRestricted || session.IsAdmin
                ? null
                : LimitRepository.GetEffective(user.Id, today);
            if (user is null || limit is null)
            {
                // Limits removed while locked out: let the user back in
                if (session.LockedOutAt is not null) _sessionTracker.SetLockedOut(sessionId, null);
                return;
            }

            var usage = UsageRepository.GetUsage(user.Id, today);
            var allowed = UsageRecord.AllowedMinutes(limit, usage);
            var minutesUsed = usage?.MinutesUsed ?? 0;
            var limitReached = minutesUsed >= allowed && usage is not null;
            var outsideSchedule = now < limit.ScheduleStart || now >= limit.ScheduleEnd;
            var reason = limitReached ? "Daily limit reached" : outsideSchedule ? "Outside allowed schedule" : null;

            switch (LockoutPolicy.Decide(reason is not null, session.LockedOutAt, session.IsPaused, dateTimeNow))
            {
                case LockoutAction.None:
                    if (reason is null) WarnIfLogoffIsNear(sessionId, session, limit, allowed, minutesUsed, now);
                    break;

                case LockoutAction.LiftLockout:
                    _sessionTracker.SetLockedOut(sessionId, null);
                    _logger.Information("Lockout lifted for {Username}: more time available", session.Username);
                    EventRepository.LogEvent(session.UserSid, EventType.LOCKED_OUT, "Lockout lifted: more time available");
                    WarnIfLogoffIsNear(sessionId, session, limit, allowed, minutesUsed, now);
                    break;

                case LockoutAction.LockOut:
                    if (limitReached)
                    {
                        _logger.Information("Daily limit reached for {Username} ({MinutesUsed}/{Allowed} min, bonus {Bonus})",
                            session.Username, minutesUsed, allowed, usage?.BonusMinutes ?? 0);
                        EventRepository.LogEvent(session.UserSid, EventType.LIMIT_REACHED,
                            $"Used {minutesUsed} of {allowed} minutes");
                    }
                    else
                    {
                        _logger.Information("Outside allowed schedule for {Username} (allowed {Start}-{End})",
                            session.Username, limit.ScheduleStart, limit.ScheduleEnd);
                    }

                    _sessionTracker.SetLockedOut(sessionId, dateTimeNow);
                    SessionManager.DisconnectSession(sessionId);
                    EventRepository.LogEvent(session.UserSid, EventType.LOCKED_OUT,
                        $"{reason}; logoff in {LockoutPolicy.GracePeriod.TotalMinutes:0} min unless more time is granted");
                    break;

                case LockoutAction.RelockAfterNotice:
                    RelockAfterNotice(sessionId, session, reason!);
                    break;

                case LockoutAction.Logoff:
                    _logger.Information("Logging off {Username}: {Reason}, locked out since {LockedOutAt:HH:mm}",
                        session.Username, reason, session.LockedOutAt);
                    SessionManager.ForceLogoff(sessionId);
                    EventRepository.LogEvent(session.UserSid, EventType.FORCED_LOGOUT,
                        $"{reason}; locked out for {LockoutPolicy.GracePeriod.TotalMinutes:0} min");
                    _sessionTracker.RemoveSession(sessionId);
                    break;
            }
        }
    }

    /// <summary>
    /// The user signed back in while still out of time: show why, then disconnect again a few seconds later
    /// (a message in a session that is disconnected right away would never be seen).
    /// </summary>
    private void RelockAfterNotice(int sessionId, ActiveSession session, string reason)
    {
        var now = DateTime.Now;
        if (_relockPending.TryGetValue(sessionId, out var pendingSince) && now - pendingSince < RelockDelay * 2)
            return;
        _relockPending[sessionId] = now;

        _logger.Information("{Username} signed back in while locked out ({Reason}); locking again", session.Username, reason);
        EventRepository.LogEvent(session.UserSid, EventType.LOCKED_OUT, $"{reason}; signed back in, locked again");

        var (title, message) = LockoutPolicy.BuildRelockMessage();
        SessionManager.SendMessage(sessionId, title, message);

        Task.Run(async () =>
        {
            await Task.Delay(RelockDelay);
            _relockPending.TryRemove(sessionId, out _);
            if (_sessionTracker.ActiveSessions.TryGetValue(sessionId, out var current) && current.LockedOutAt is not null)
            {
                SessionManager.DisconnectSession(sessionId);
            }
        });
    }

    private void WarnIfLogoffIsNear(
        int sessionId, ActiveSession session, LimitConfig limit, int allowedMinutes, int minutesUsed, TimeOnly now)
    {
        // Nobody would see a message in a locked or disconnected session; it is shown after the session resumes.
        if (session.IsPaused) return;

        var remaining = UsageWarnings.GetRemainingMinutes(allowedMinutes, minutesUsed, limit.ScheduleEnd, now);
        var (warnThreshold, newState) = UsageWarnings.Evaluate(remaining, session.WarnedThreshold);
        if (newState != session.WarnedThreshold)
        {
            _sessionTracker.SetWarnedThreshold(sessionId, newState);
        }

        if (warnThreshold is null) return;

        var (title, message) = UsageWarnings.BuildMessage(remaining);
        if (SessionManager.SendMessage(sessionId, title, message))
        {
            _logger.Information("Logoff warning sent to {Username}: {Remaining} min remaining",
                session.Username, remaining);
            EventRepository.LogEvent(session.UserSid, EventType.WARNING_SENT, $"{remaining} min remaining");
        }
        else
        {
            _logger.Warning("Failed to send logoff warning to {Username} (session {SessionId}), Win32 error {Error}",
                session.Username, sessionId, Marshal.GetLastPInvokeError());
        }
    }

    private void RunCleanupIfNeeded()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today <= _lastCleanupDate) return;

        _lastCleanupDate = today;

        var eventsCutoff = DateTime.Now.AddDays(-DatabaseManager.RetentionDays);
        var usageCutoff = today.AddDays(-DatabaseManager.RetentionDays);

        var eventsDeleted = EventRepository.DeleteOlderThan(eventsCutoff)
            + EventRepository.TrimToMaxCount(DatabaseManager.MaxEvents);
        var usageDeleted = UsageRepository.DeleteOlderThan(usageCutoff);

        if (eventsDeleted > 0 || usageDeleted > 0)
        {
            _logger.Information("Database cleanup: deleted {EventsDeleted} events and {UsageDeleted} usage records older than {Days} days",
                eventsDeleted, usageDeleted, DatabaseManager.RetentionDays);
        }
    }
}
