﻿using System;

namespace CYarp.Server.Configs
{
    /// <summary>
    /// 客户端配置
    /// </summary>
    public class ClientConfig
    {
        /// <summary>
        /// 获取或设置是否启用 KeepAlive 功能
        /// 默认true
        /// </summary>
        public bool KeepAlive { get; set; } = true;

        /// <summary>
        /// 获取或设置心跳包周期
        /// 默认50s
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(50d);
    }
}
