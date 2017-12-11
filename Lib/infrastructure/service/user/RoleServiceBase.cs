﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lib.infrastructure.entity;
using Lib.data;
using Lib.mvc;
using Lib.helper;
using Lib.extension;
using System.Data.Entity;
using Lib.core;
using Lib.mvc.user;
using Lib.cache;
using Lib.infrastructure.extension;
using Lib.data.ef;

namespace Lib.infrastructure.service.user
{
    public interface IRoleServiceBase<RoleBase, UserRoleBase, RolePermissionBase>
        where RoleBase : RoleEntityBase, new()
        where RolePermissionBase : RolePermissionEntityBase, new()
        where UserRoleBase : UserRoleEntityBase, new()
    { }

    public abstract class RoleServiceBase<RoleBase, UserRoleBase, RolePermissionBase> :
        IRoleServiceBase<RoleBase, UserRoleBase, RolePermissionBase>
        where RoleBase : RoleEntityBase, new()
        where RolePermissionBase : RolePermissionEntityBase, new()
        where UserRoleBase : UserRoleEntityBase, new()
    {
        protected readonly IEFRepository<RoleBase> _roleRepo;
        protected readonly IEFRepository<UserRoleBase> _userRoleRepo;
        protected readonly IEFRepository<RolePermissionBase> _rolePermissionRepo;

        public RoleServiceBase(
            IEFRepository<RoleBase> _roleRepo,
            IEFRepository<UserRoleBase> _userRoleRepo,
            IEFRepository<RolePermissionBase> _rolePermissionRepo)
        {
            this._roleRepo = _roleRepo;
            this._userRoleRepo = _userRoleRepo;
            this._rolePermissionRepo = _rolePermissionRepo;
        }

        public virtual async Task<List<RoleBase>> QueryRoleList(string parent = null) =>
            await this._roleRepo.QueryNodeList(parent);

        public virtual async Task<_<string>> AddRole(RoleBase role) => await this._roleRepo.AddTreeNode(role, "role");

        public virtual async Task<_<string>> DeleteRoleRecursively(string role_uid) =>
            await this._roleRepo.DeleteTreeNodeRecursively(role_uid);

        public virtual async Task<_<string>> DeleteRole(params string[] role_uids) =>
            await this._roleRepo.DeleteByMultipleUIDS(role_uids);

        public abstract void UpdateRoleEntity(ref RoleBase old_role, ref RoleBase new_role);

        public virtual async Task<_<string>> UpdateRole(RoleBase model)
        {
            var data = new _<string>();

            var role = await this._roleRepo.GetFirstAsync(x => x.UID == model.UID);
            Com.AssertNotNull(role, $"角色不存在：{model.UID}");
            this.UpdateRoleEntity(ref role, ref model);
            role.Update();
            if (!role.IsValid(out var msg))
            {
                data.SetErrorMsg(msg);
                return data;
            }
            if (await this._roleRepo.UpdateAsync(role) > 0)
            {
                data.SetSuccessData(string.Empty);
                return data;
            }

            throw new Exception("更新角色失败");
        }
        public virtual async Task<_<string>> SetUserRoles(string user_uid, List<UserRoleBase> roles)
        {
            var data = new _<string>();
            if (ValidateHelper.IsPlumpList(roles))
            {
                if (roles.Any(x => x.UserID != user_uid))
                {
                    data.SetErrorMsg("用户ID错误");
                    return data;
                }
            }
            await this._userRoleRepo.DeleteWhereAsync(x => x.UserID == user_uid);
            if (ValidateHelper.IsPlumpList(roles))
            {
                foreach (var m in roles)
                {
                    m.Init("ur");
                    if (!m.IsValid(out var msg))
                    {
                        data.SetErrorMsg(msg);
                        return data;
                    }
                }
                if (await this._userRoleRepo.AddAsync(roles.ToArray()) <= 0)
                {
                    data.SetErrorMsg("保存角色错误");
                    return data;
                }
            }

            data.SetSuccessData(string.Empty);
            return data;
        }

        public virtual async Task<_<string>> SetRolePermissions(string role_uid, List<RolePermissionBase> permissions)
        {
            var data = new _<string>();
            if (ValidateHelper.IsPlumpList(permissions))
            {
                if (permissions.Any(x => x.RoleID != role_uid))
                {
                    data.SetErrorMsg("角色ID错误");
                    return data;
                }
            }
            await this._rolePermissionRepo.DeleteWhereAsync(x => x.RoleID == role_uid);
            if (ValidateHelper.IsPlumpList(permissions))
            {
                foreach (var m in permissions)
                {
                    m.Init("per");
                    if (!m.IsValid(out var msg))
                    {
                        data.SetErrorMsg(msg);
                        return data;
                    }
                }
                if (await this._rolePermissionRepo.AddAsync(permissions.ToArray()) <= 0)
                {
                    data.SetErrorMsg("保存权限错误");
                    return data;
                }
            }

            data.SetSuccessData(string.Empty);
            return data;
        }

        public async Task<_<string>> SetRolePermissions(string role_uid, string[] permissions)
        {
            var data = new _<string>();

            var old_permissions = new string[] { };

            var update = old_permissions.UpdateList(permissions);

            var dead_permissions = update.WaitForDelete;
            var new_permissions = update.WaitForAdd;

            //add new
            //delete old

            await Task.FromResult(1);

            return data;
        }
    }
}
