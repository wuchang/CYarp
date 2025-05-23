﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CYarp.Server.Clients
{
    /// <summary>
    /// Tunnel工厂
    /// </summary> 
    sealed class TunnelFactory
    {
        private readonly ConcurrentDictionary<TunnelId, TaskCompletionSource<Tunnel>> tunnelCompletionSources = new();

        public ILogger<Tunnel> Logger { get; }

        public TunnelFactory(ILogger<Tunnel> logger)
        {
            this.Logger = logger;
        }

        /// <summary>
        /// 创建Tunnel
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Tunnel> CreateTunnelAsync(ClientConnection connection, CancellationToken cancellationToken)
        {
            var tunnelId = connection.NewTunnelId();
            var tunnelSource = new TaskCompletionSource<Tunnel>();
            if (this.tunnelCompletionSources.TryAdd(tunnelId, tunnelSource) == false)
            {
                throw new SystemException($"系统中已存在{tunnelId}的tunnelId");
            }

            try
            {
                TunnelLog.LogTunnelCreating(this.Logger, connection.ClientId, tunnelId);
                await connection.CreateTunnelAsync(tunnelId, cancellationToken);
                return await tunnelSource.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TunnelLog.LogTunnelCreateFailure(this.Logger, connection.ClientId, tunnelId, "远程端操作超时");
                throw;
            }
            catch (Exception ex)
            {
                TunnelLog.LogTunnelCreateFailure(this.Logger, connection.ClientId, tunnelId, ex.Message);
                throw;
            }
            finally
            {
                this.tunnelCompletionSources.TryRemove(tunnelId, out _);
            }
        }

        public bool Contains(TunnelId tunnelId)
        {
            return this.tunnelCompletionSources.ContainsKey(tunnelId);
        }

        public bool SetResult(Tunnel httpTunnel)
        {
            return this.tunnelCompletionSources.TryRemove(httpTunnel.Id, out var source) && source.TrySetResult(httpTunnel);
        }
    }
}
