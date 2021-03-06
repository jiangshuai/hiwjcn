﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lib.mvc.user;
using Lib.core;
using Lib.extension;
using Lib.helper;
using System.ComponentModel.DataAnnotations;

namespace Lib.infrastructure.entity
{
    /// <summary>
    /// 导航/边栏/轮播都属于菜单
    /// </summary>
    [Serializable]
    public class MenuEntityBase : TreeEntityBase
    {
        private readonly Lazy_<List<string>> PermissionValues;

        public MenuEntityBase()
        {
            this.PermissionValues = new Lazy_<List<string>>(() => this.PermissionJson?.JsonToEntity<List<string>>(throwIfException: false));
        }

        [Required]
        public virtual string MenuName { get; set; }

        public virtual string Description { get; set; }

        public virtual string Url { get; set; }

        public virtual string ImageUrl { get; set; }

        public virtual string IconCls { get; set; }

        public virtual string IconUrl { get; set; }

        public virtual string HtmlId { get; set; }

        public virtual string HtmlCls { get; set; }

        public virtual string HtmlStyle { get; set; }

        public virtual string HtmlOnClick { get; set; }

        public virtual int Sort { get; set; }

        public virtual string PermissionJson { get; set; }

        [Required]
        public virtual string MenuGroupKey { get; set; }

        public virtual bool ShowForUser(LoginUserInfo loginuser)
        {
            var pers = this.PermissionValues.Value;
            if (ValidateHelper.IsPlumpList(pers))
            {
                foreach (var p in pers)
                {
                    if (!(loginuser?.HasPermission(p) ?? false))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }

    [Serializable]
    public class MenuGroupEntityBase : BaseEntity
    {
        [Required]
        public virtual string GroupKey { get; set; }

        [Required]
        public virtual string GroupName { get; set; }

        public virtual string Description { get; set; }

        public virtual string ImageUrl { get; set; }
    }
}
