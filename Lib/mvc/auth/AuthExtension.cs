﻿using Lib.helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Lib.extension;
using Lib.io;
using Lib.ioc;
using Lib.mvc.user;
using System.Reflection;
using System.Web.SessionState;
using Autofac;
using Lib.mvc.auth.validation;

namespace Lib.mvc.auth
{
    public static class AuthExtension
    {
        public const string AuthedUserKey = "auth.user.entity";

        /// <summary>
        /// 获取auth上下文
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static AuthContext AuthContext(this HttpContext context)
        {
            if (!AppContext.IsRegistered<ITokenProvider>())
            {
                throw new Exception("请注册token provider");
            }
            var tokenProvider = AppContext.GetObject<ITokenProvider>();
            return new AuthContext(context, tokenProvider);
        }

        /// <summary>
        /// 获取当前登录用户
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static async Task<LoginUserInfo> GetAuthUserAsync(this HttpContext context)
        {
            var data = await context.CacheInHttpContextAsync(AuthedUserKey, async () =>
            {
                if (!AppContext.IsRegistered<TokenValidationProviderBase>())
                {
                    throw new Exception($"没有注册{nameof(TokenValidationProviderBase)}");
                }
                var validator = AppContext.GetObject<TokenValidationProviderBase>();
                return await validator.FindUserAsync(context);
            });
            return data;
        }

        /// <summary>
        /// 获取当前登录用户
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static LoginUserInfo GetAuthUser(this HttpContext context)
        {
            var data = context.CacheInHttpContext(AuthedUserKey, () =>
            {
                if (!AppContext.IsRegistered<TokenValidationProviderBase>())
                {
                    throw new Exception($"没有注册{nameof(TokenValidationProviderBase)}");
                }
                var validator = AppContext.GetObject<TokenValidationProviderBase>();
                return validator.FindUser(context);
            });
            return data;
        }

        /// <summary>
        /// 获取bearer token
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetBearerToken(this HttpContext context)
        {
            var bearer = "Bearer" + ' '.ToString();
            var token = context.Request.Headers["Authorization"] ?? string.Empty;
            if (token.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            {
                return token.Substring(bearer.Length);
            }
            return string.Empty;
        }

        /// <summary>
        /// 使用auth server验证
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="config"></param>
        public static void AuthUseAuthServerValidation(this ContainerBuilder builder, Func<AuthServerConfig> config)
        {
            builder.Register(_ => config.Invoke()).AsSelf().SingleInstance();
            builder.RegisterType<AuthServerValidationProvider>().AsSelf().As<TokenValidationProviderBase>().SingleInstance();
        }

        /// <summary>
        /// 使用token验证
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="config"></param>
        public static void AuthUseCookieValidation(this ContainerBuilder builder, Func<LoginStatus> config)
        {
            builder.Register(_ => config.Invoke()).AsSelf().SingleInstance();
            builder.RegisterType<CookieTokenValidationProvider>().AsSelf().As<TokenValidationProviderBase>().SingleInstance();
        }

        /// <summary>
        /// 自定义验证
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="config"></param>
        public static void AuthUseCustomValidation(this ContainerBuilder builder, Func<TokenValidationProviderBase> config)
        {
            builder.Register(_ => config.Invoke()).AsSelf().As<TokenValidationProviderBase>().SingleInstance();
        }
    }
}