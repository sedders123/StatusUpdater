using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace status_updater
{
    public enum StatusType
    {
        None,
        Music,
        Meeting,
        Call
    }

    public static class Extensions
    {
        public static T Next<T>(this T src) where T : struct
        {
            if (!typeof(T).IsEnum) throw new ArgumentException($"Argument {typeof(T).FullName} is not an Enum");

            var arr = (T[]) Enum.GetValues(src.GetType());
            var j = Array.IndexOf(arr, src) + 1;
            return arr.Length == j ? arr[0] : arr[j];
        }

        public static T Previous<T>(this T src) where T : struct
        {
            if (!typeof(T).IsEnum) throw new ArgumentException($"Argument {typeof(T).FullName} is not an Enum");

            var arr = (T[]) Enum.GetValues(src.GetType());
            var j = Array.IndexOf(arr, src) - 1;
            return j < 0 ? arr[0] : arr[j];
        }
    }

    public class StatusManager
    {
        public bool LastNotificationToSlackFailed { get; private set; }
        public DateTime? LastNotificationSentToSlack { get; private set; }

        private readonly SemaphoreSlim _statusLock = new SemaphoreSlim(1);

        private readonly SlackService _service;
        private StatusType _currentStatusType = StatusType.Music;

        private readonly Dictionary<StatusType, (string emoji, string Status)> _currentStatuses = new Dictionary<StatusType, (string emoji, string Status)>
        {
            {StatusType.None, (null, null)}
        };

        public StatusManager(SlackService service)
        {
            _service = service;
        }


        public async Task<bool> SetStatusAsync(string emoji, string status, StatusType type)
        {
            await _statusLock.WaitAsync();
            try
            {
                if (_currentStatuses.ContainsKey(type))
                {
                    var (currentEmoji, currentStatus) = _currentStatuses[type];
                    if (currentEmoji == emoji && currentStatus == status)
                    {
                        return true;
                    }
                }

                _currentStatuses[type] = (emoji, status);
                if (type >= _currentStatusType)
                {
                    _currentStatusType = type;
                    return await SetSlackStatusAsync(emoji, status);
                }
                return true;
            }
            finally
            {
                _statusLock.Release(1);
            }

        }

        private async Task<bool> SetSlackStatusAsync(string emoji, string status)
        {
            var notified = await _service.SetUserStatusAsync(emoji, status);
            LastNotificationToSlackFailed = !notified;
            LastNotificationSentToSlack = DateTime.UtcNow;
            return notified;
        }

        public async Task SyncStatus()
        {
            await _statusLock.WaitAsync();
            try
            {
                var (emoji, status) = _currentStatuses[_currentStatusType];
                await SetSlackStatusAsync(emoji, status);
            }
            finally
            {
                _statusLock.Release(1);
            }
        }

        public async Task SetStatusCompleteAsync(StatusType type)
        {
            await _statusLock.WaitAsync();
            try
            {
                if (_currentStatusType != type)
                {
                    return;
                }

                _currentStatuses[type] = (null, null);

                _currentStatusType = type.Previous();
                var (emoji, status) = _currentStatuses[_currentStatusType];
                await SetSlackStatusAsync(emoji, status);
            }
            finally
            {
                _statusLock.Release(1);
            }

        }
    }
}