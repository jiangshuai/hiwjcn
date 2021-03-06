﻿using Hiwjcn.Bll.Auth;
using Hiwjcn.Core;
using Hiwjcn.Core.Domain.Auth;
using Hiwjcn.Framework;
using Lib.cache;
using Lib.data;
using Lib.helper;
using Lib.infrastructure.service;
using Lib.extension;
using Lib.mvc;
using Lib.mvc.auth;
using Lib.mvc.auth.validation;
using Lib.mvc.user;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using WebCore.MvcLib.Controller;
using Lib.data.ef;
using Hiwjcn.Core.Domain.User;
using Lib.infrastructure.service.user;
using Lib.infrastructure.extension;

namespace Hiwjcn.Web.Controllers
{
    public class AccountController : UserBaseController
    {
        private readonly IAuthLoginService _IAuthLoginService;
        private readonly IAuthTokenService _IAuthTokenService;
        private readonly LoginStatus _LoginStatus;
        private readonly IEFRepository<AuthScope> _AuthScopeRepo;
        private readonly IEFRepository<LoginErrorLogEntity> _LogErrorRepo;
        private readonly IAuthDataProvider _IValidationDataProvider;
        private readonly ICacheProvider _cache;

        public AccountController(
            IAuthLoginService _IAuthLoginService,
            IAuthTokenService _IAuthTokenService,
            LoginStatus logincontext,
            IEFRepository<AuthScope> _AuthScopeRepo,
            IEFRepository<LoginErrorLogEntity> _LogErrorRepo,
            IAuthDataProvider _IValidationDataProvider,
            ICacheProvider _cache)
        {
            this._IAuthLoginService = _IAuthLoginService;
            this._IAuthTokenService = _IAuthTokenService;
            this._LoginStatus = logincontext;
            this._AuthScopeRepo = _AuthScopeRepo;
            this._LogErrorRepo = _LogErrorRepo;
            this._IValidationDataProvider = _IValidationDataProvider;
            this._cache = _cache;
        }

        #region 登录
        [NonAction]
        private string retry_count_cache_key(string key) => $"login.retry.count.{key}".WithCacheKeyPrefix();

        [NonAction]
        private async Task<string> AntiRetry(string user_name, Func<Task<string>> func)
        {
            if (!ValidateHelper.IsPlumpString(user_name)) { throw new Exception("username为空"); }

            var cache_key = this.retry_count_cache_key(user_name);
            var expire = 5;
            var max_error = 3;
            var now = DateTime.Now;

            var list = this._cache.Get<List<DateTime>>(cache_key).Result ?? new List<DateTime>() { };
            list = list.Where(x => x > now.AddMinutes(-expire)).ToList();

            try
            {
                if (list.Count > max_error)
                {
                    return "错误尝试过多";
                }
                //执行登录
                var data = await func.Invoke();
                if (ValidateHelper.IsPlumpString(data))
                {
                    list.Add(now);
                }
                return data;
            }
            finally
            {
                this._cache.Set(cache_key, list, TimeSpan.FromMinutes(expire));
            }
        }

        [NonAction]
        private async Task<_<LoginUserInfo>> CreateAuthToken(LoginUserInfo loginuser)
        {
            var data = new _<LoginUserInfo>();
            if (loginuser == null)
            {
                data.SetErrorMsg("登录失败");
                return data;
            }

            var client_id = this._IValidationDataProvider.GetClientID(this.X.context);
            var client_security = this._IValidationDataProvider.GetClientSecurity(this.X.context);

            var allscopes = await this._AuthScopeRepo.GetListAsync(null);

            var token = await this._IAuthTokenService.CreateTokenAsync(client_id, client_security, loginuser.UserID, allscopes.Select(x => x.Name).ToList());
            if (token.error)
            {
                data.SetErrorMsg(token.msg);
                return data;
            }

            loginuser.SetAuthToken(token.data);

            data.SetSuccessData(loginuser);
            return data;
        }

        [NonAction]
        private async Task<_<LoginUserInfo>> CreateAuthTokenWithCode(LoginUserInfo loginuser)
        {
            var data = new _<LoginUserInfo>();
            if (loginuser == null)
            {
                data.SetErrorMsg("登录失败");
                return data;
            }

            var client_id = this._IValidationDataProvider.GetClientID(this.X.context);
            var client_security = this._IValidationDataProvider.GetClientSecurity(this.X.context);

            var allscopes = await this._AuthScopeRepo.GetListAsync(null);
            var code = await this._IAuthTokenService.CreateCodeAsync(client_id, allscopes.Select(x => x.Name).ToList(), loginuser.UserID);

            if (code.error)
            {
                data.SetErrorMsg(code.msg);
                return data;
            }

            var token = await this._IAuthTokenService.CreateTokenAsync(client_id, client_security, code.data.UID);
            if (token.error)
            {
                data.SetErrorMsg(token.msg);
                return data;
            }

            loginuser.SetAuthToken(token.data);

            data.SetSuccessData(loginuser);
            return data;
        }

        [HttpPost]
        [RequestLog]
        public async Task<ActionResult> LoginByPassword(string username, string password)
        {
            return await RunActionAsync(async () =>
            {
                var res = await this.AntiRetry(username, async () =>
                {
                    var data = await this._IAuthLoginService.LoginByPassword(username, password);
                    if (!data.success)
                    {
                        return data.msg;
                    }
                    var loginuser = await this.CreateAuthTokenWithCode(data.data);
                    if (!loginuser.success)
                    {
                        return loginuser.msg;
                    }
                    this._cache.Remove(CacheKeyManager.AuthUserInfoKey(loginuser.data.UserID));
                    this.X.context.CookieLogin(loginuser.data);
                    return string.Empty;
                });
                return GetJsonRes(res);
            });
        }

        [HttpPost]
        [RequestLog]
        public async Task<ActionResult> LoginByOneTimeCode(string phone, string code)
        {
            return await RunActionAsync(async () =>
            {
                var res = await this.AntiRetry(phone, async () =>
                {
                    var data = await this._IAuthLoginService.LoginByCode(phone, code);
                    if (!data.success)
                    {
                        return data.msg;
                    }
                    var loginuser = await this.CreateAuthTokenWithCode(data.data);
                    if (!data.success)
                    {
                        return loginuser.msg;
                    }
                    this._cache.Remove(CacheKeyManager.AuthUserInfoKey(loginuser.data.UserID));
                    this.X.context.CookieLogin(loginuser.data);
                    return string.Empty;
                });
                return GetJsonRes(res);
            });
        }

        [HttpPost]
        [RequestLog]
        public async Task<ActionResult> SendOneTimeCode(string phone)
        {
            return await RunActionAsync(async () =>
            {
                var data = await this._IAuthLoginService.SendOneTimeCode(phone);

                return GetJsonRes(data.msg);
            });
        }

        /// <summary>
        /// 登录账户
        /// </summary>
        /// <returns></returns>
        [RequestLog]
        public async Task<ActionResult> Login(string url, string @continue, string next, string callback)
        {
            return await RunActionAsync(async () =>
            {
                url = new string[] { url, @continue, next, callback, "/" }.FirstNotEmpty_();

                var auth_user = await this.X.context.GetAuthUserAsync();
                if (auth_user != null)
                {
                    this._LoginStatus.SetUserLogin(this.X.context, auth_user);
                    return Redirect(url);
                }

                return View();
            });
        }

        /// <summary>
        /// 退出地址
        /// </summary>
        /// <returns></returns>
        [RequestLog]
        public ActionResult LogOut(string url, string @continue, string next, string callback)
        {
            return RunAction(() =>
            {
                _LoginStatus.SetUserLogout();

                url = new string[] { url, @continue, next, callback, "/" }.FirstNotEmpty_();

                return Redirect(url);

            });
        }

        [RequestLog]
        public async Task<ActionResult> LoginUser()
        {
            return await RunActionAsync(async () =>
            {
                var loginuser = await this.X.context.GetAuthUserAsync();

                return GetJsonp(new _() { success = loginuser != null, data = loginuser });
            });
        }

        #endregion
    }
}
