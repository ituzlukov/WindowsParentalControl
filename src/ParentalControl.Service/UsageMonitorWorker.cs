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
    private DateOnly _lastCleanupDate = DateOnly.MinValue;

    public UsageMonitorWorker(SessionTracker sessionTracker, Serilog.ILogger logger)
    {
        _sessionTracker = sessionTracker;
        _logger = logger;
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

        var today = DateOnly.FromDateTime(DateTime.Now);
        var now = TimeOnly.FromDateTime(DateTime.Now);

        foreach (var (sessionId, session) in _sessionTracker.ActiveSessions)
        {
            var user = UserRepository.GetBySid(session.UserSid);
            if (user is null || !user.IsRestricted || session.IsAdmin) continue;

            var limit = LimitRepository.GetEffective(user.Id, today);
            if (limit is null) continue;

            var usage = UsageRepository.GetUsage(user.Id, today);
            var allowed = UsageRecord.AllowedMinutes(limit, usage);
            if (usage is not null && usage.MinutesUsed >= allowed)
            {
                _logger.Information("Daily limit reached for {Username} ({MinutesUsed}/{Allowed} min, bonus {Bonus})",
                    session.Username, usage.MinutesUsed, allowed, usage.BonusMinutes);
                EventRepository.LogEvent(session.UserSid, EventType.LIMIT_REACHED,
                    $"Used {usage.MinutesUsed} of {allowed} minutes");
                SessionManager.ForceLogoff(sessionId);
                EventRepository.LogEvent(session.UserSid, EventType.FORCED_LOGOUT, "Daily limit reached");
                _sessionTracker.RemoveSession(sessionId);
                continue;
            }

            if (now < limit.ScheduleStart || now >= limit.ScheduleEnd)
            {
                _logger.Information("Outside allowed schedule for {Username} (allowed {Start}-{End})",
                    session.Username, limit.ScheduleStart, limit.ScheduleEnd);
                SessionManager.ForceLogoff(sessionId);
                EventRepository.LogEvent(session.UserSid, EventType.FORCED_LOGOUT, "Outside allowed schedule");
                _sessionTracker.RemoveSession(sessionId);
                continue;
            }

            WarnIfLogoffIsNear(sessionId, session, limit, allowed, usage?.MinutesUsed ?? 0, now);
        }
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
