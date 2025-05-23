﻿using CYarp.Server.Clients;
using CYarp.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace CYarp.Server.Middlewares
{
    /// <summary>
    /// IClient的的处理者
    /// </summary>
    sealed class ClientHandler
    {
        private const string CYarpTargetUriHeader = "CYarp-TargetUri";
        private readonly Func<HttpContext, ValueTask<string?>> clientIdProvider;

        /// <summary>
        /// IClient的的处理者
        /// </summary>
        /// <param name="clientIdProvider"></param>
        public ClientHandler(Func<HttpContext, ValueTask<string?>> clientIdProvider)
        {
            this.clientIdProvider = clientIdProvider;
        }

        public async Task<IResult> HandleClientAsync(
            HttpContext context,
            IHttpForwarder httpForwarder,
            TunnelFactory tunnelFactory,
            ClientManager clientManager,
            IOptionsMonitor<CYarpOptions> yarpOptions,
            ILogger<Client> logger)
        {
            var cyarpFeature = context.Features.GetRequiredFeature<ICYarpFeature>();
            if (cyarpFeature.IsCYarpRequest == false)
            {
                ClientLog.LogInvalidRequest(logger, context.Connection.Id, "不是有效的CYarp请求");
                return Results.BadRequest();
            }

            if (cyarpFeature.IsCYarpRequest == false || context.Request.Headers.TryGetValue(CYarpTargetUriHeader, out var targetUri) == false)
            {
                ClientLog.LogInvalidRequest(logger, context.Connection.Id, $"请求头{CYarpTargetUriHeader}不存在");
                return Results.BadRequest();
            }

            // CYarp-TargetUri头格式验证
            if (Uri.TryCreate(targetUri, UriKind.Absolute, out var clientTargetUri) == false)
            {
                ClientLog.LogInvalidRequest(logger, context.Connection.Id, $"请求头{CYarpTargetUriHeader}的值不是Uri格式");
                return Results.BadRequest();
            }

            // 查找clientId
            var clientId = await this.clientIdProvider.Invoke(context);
            if (string.IsNullOrEmpty(clientId))
            {
                ClientLog.LogInvalidRequest(logger, context.Connection.Id, "无法获取到IClient的Id");
                return Results.Forbid();
            }

            var options = yarpOptions.CurrentValue;
            var stream = await cyarpFeature.AcceptAsSafeWriteStreamAsync();
            var connection = new ClientConnection(clientId, stream, options.Client, logger);
            await using (var client = new Client(connection, httpForwarder, tunnelFactory, options.HttpTunnel, clientTargetUri, cyarpFeature.Protocol, context.User))
            {
                var httpConnection = context.Connection;
                if (httpConnection != null && httpConnection.RemoteIpAddress != null)
                {
                    client.RemoteEndpoint = new IPEndPoint(httpConnection.RemoteIpAddress, httpConnection.RemotePort);
                }

                if (await clientManager.AddAsync(client))
                {
                    ClientLog.LogConnected(logger, clientId, cyarpFeature.Protocol, clientManager.Count);
                    await client.WaitForCloseAsync();

                    if (await clientManager.RemoveAsync(client))
                    {
                        ClientLog.LogDisconnected(logger, clientId, cyarpFeature.Protocol, clientManager.Count);
                    }
                }
            }

            // 关闭连接
            context.Abort();
            return Results.Empty;
        }
    }
}
