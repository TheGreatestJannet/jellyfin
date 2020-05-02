using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Server.Implementations.Activity
{
    /// <summary>
    /// Manages the storage and retrieval of <see cref="ActivityLog"/> instances.
    /// </summary>
    public class ActivityManager : IActivityManager
    {
        private JellyfinDbProvider _provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityManager"/> class.
        /// </summary>
        /// <param name="provider">The Jellyfin database provider.</param>
        public ActivityManager(JellyfinDbProvider provider)
        {
            _provider = provider;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<ActivityLogEntry>> EntryCreated;

        /// <inheritdoc/>
        public void Create(ActivityLog entry)
        {
            using var dbContext = _provider.CreateContext();
            dbContext.ActivityLogs.Add(entry);
            dbContext.SaveChanges();

            EntryCreated?.Invoke(this, new GenericEventArgs<ActivityLogEntry>(ConvertToOldModel(entry)));
        }

        /// <inheritdoc/>
        public async Task CreateAsync(ActivityLog entry)
        {
            using var dbContext = _provider.CreateContext();
            await dbContext.ActivityLogs.AddAsync(entry);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            EntryCreated?.Invoke(this, new GenericEventArgs<ActivityLogEntry>(ConvertToOldModel(entry)));
        }

        /// <inheritdoc/>
        public QueryResult<ActivityLogEntry> GetPagedResult(
            Func<IQueryable<ActivityLog>, IEnumerable<ActivityLog>> func,
            int? startIndex,
            int? limit)
        {
            using var dbContext = _provider.CreateContext();

            var result = func.Invoke(dbContext.ActivityLogs).AsQueryable();

            if (startIndex.HasValue)
            {
                result = result.Where(entry => entry.Id >= startIndex.Value);
            }

            if (limit.HasValue)
            {
                result = result.OrderByDescending(entry => entry.DateCreated).Take(limit.Value);
            }

            // This converts the objects from the new database model to the old for compatibility with the existing API.
            var list = result.Select(entry => ConvertToOldModel(entry)).ToList();

            return new QueryResult<ActivityLogEntry>()
            {
                Items = list,
                TotalRecordCount = list.Count
            };
        }

        /// <inheritdoc/>
        public QueryResult<ActivityLogEntry> GetPagedResult(int? startIndex, int? limit)
        {
            return GetPagedResult(logs => logs, startIndex, limit);
        }

        private static ActivityLogEntry ConvertToOldModel(ActivityLog entry)
        {
            return new ActivityLogEntry
            {
                Id = entry.Id,
                Name = entry.Name,
                Overview = entry.Overview,
                ShortOverview = entry.ShortOverview,
                Type = entry.Type,
                ItemId = entry.ItemId,
                UserId = entry.UserId,
                Date = entry.DateCreated,
                Severity = entry.LogSeverity
            };
        }
    }
}
