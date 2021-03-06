﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lib.infrastructure.entity;
using Lib.infrastructure.entity.common;
using Lib.data;
using Lib.mvc;
using Lib.helper;
using Lib.extension;
using System.Data.Entity;
using Lib.core;
using Lib.infrastructure.extension;
using Lib.data.ef;

namespace Lib.infrastructure.service.common
{
    public interface IEventLogServiceBase<LogBase>
    {
        Task AddLogAsync(LogBase model);

        void AddLog(LogBase model);

        void ClearOldLogs(DateTime before);

        Task<PagerData<LogBase>> QueryPager(string q = null,
             DateTime? start = null, DateTime? end = null, int page = 1, int pagesize = 10);
    }

    public abstract class EventLogServiceBase<LogBase> :
        IEventLogServiceBase<LogBase>
        where LogBase : EventLogEntityBase
    {
        protected readonly IEFRepository<LogBase> _logRepo;

        public EventLogServiceBase(IEFRepository<LogBase> _logRepo)
        {
            this._logRepo = _logRepo;
        }

        public virtual async Task AddLogAsync(LogBase model) => await this._logRepo.AddEntity_(model, "log");

        public virtual void AddLog(LogBase model)
        {
            model.Init("log");
            this._logRepo.Add(model);
        }

        public virtual void ClearOldLogs(DateTime before) => this._logRepo.DeleteWhere(x => x.CreateTime < before);

        public async Task<PagerData<LogBase>> QueryPager(string q = null,
            DateTime? start = null, DateTime? end = null, int page = 1, int pagesize = 10)
        {
            return await this._logRepo.PrepareIQueryableAsync_(async query =>
            {
                if (start != null)
                {
                    query = query.Where(x => x.CreateTime >= start.Value);
                }
                if (end != null)
                {
                    end = end.Value.Date.AddDays(1);
                    query = query.Where(x => x.CreateTime < end.Value);
                }
                if (ValidateHelper.IsPlumpString(q))
                {
                    query = query.Where(x => x.Summary.Contains(q));
                }

                return await query.ToPagedListAsync(page, pagesize, x => x.CreateTime);
            });
        }
    }
}
