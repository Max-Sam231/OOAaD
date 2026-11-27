using System.Threading.Channels;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Server
{
    public static class ServerBootstrap
    {
        public static Task RunAsync(string[] args, CancellationToken ct) =>
            RunAsync(args, ct, null, null);

        public static async Task RunAsync(
            string[] args,
            CancellationToken ct,
            Action<object>? configureServerScope,
            Action<Uri>? onStarted)
        {
            new InitCommand().Execute();
            var previousScope = Ioc.Resolve<object>("IoC.Scope.Current");
            var restoreScope = Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previousScope);

            IGameRouter router = null!;
            Func<bool> isIngressPaused = null!;
            IGameConnectionRegistry connections = null!;
            ICommandMessageDecoding decoder = null!;
            ServerOptions options = null!;
            Action<string> executeHotReload = null!;
            Action<string?> executeShutdown = null!;

            try
            {
                Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
                var rootScope = Ioc.Resolve<object>("IoC.Scope.Current");
                var serverScope = Ioc.Resolve<object>("IoC.Scope.Create", rootScope);

                var serverConnections = new GameConnectionRegistry();
                var serverExceptions = new ServerExceptionRecording();
                var serverOptions = new ServerOptions();
                var emptyCommand = new DelegatingCommand(() => { });
                Func<object[], object> emptyFactory = _ => emptyCommand;

                var endpointGameBaseInitializer = new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencySnapshotCodecs().Execute();
                    new RegisterIoCDependencyMacroCommand().Execute();
                    new RegisterIoCDependencyAuthorizeCommand().Execute();
                    new RegisterIoCDependencyGameRules().Execute();
                    Register("Commands.Empty", emptyFactory);
                    new RegisterIoCDependencyEndpointGameBase(serverConnections, serverExceptions.Record).Execute();
                });

                new InSeparateScopeCommand(
                    serverScope,
                    new DelegatingCommand(() =>
                    {
                        new RegisterIoCDependencyServerThread().Execute();
                        new RegisterIoCDependencyEndpoint().Execute();
                        Register("Commands.Empty", emptyFactory);
                        Register("Server.Options", _ => serverOptions);
                        Register("Server.Exceptions", _ => serverExceptions);
                        Register(
                            "Exception.Handle",
                            exArgs => new DelegatingCommand(
                                () => serverExceptions.Record((ICommand)exArgs[0], (Exception)exArgs[1])));
                        Register("Endpoint.GameBase.Initializer", _ => endpointGameBaseInitializer);
                        new RegisterIoCDependencyScopeHierarchy(rootScope, endpointGameBaseInitializer).Execute();
                        Register(
                            "Server.Admin.HotReload",
                            hrArgs => Ioc.Resolve<ICommand>("Scopes.HotReload", (string)hrArgs[0]));
                        Register(
                            "Server.Admin.Shutdown",
                            _ => Ioc.Resolve<ICommand>("Commands.Empty"));

                        if (configureServerScope != null)
                        {
                            var dict = (IDictionary<string, Func<object[], object>>)serverScope;
                            dict["IoC.Register"] = regArgs => new DelegatingCommand(
                                () => dict[(string)regArgs[0]] = (Func<object[], object>)regArgs[1]);
                            try
                            {
                                configureServerScope(serverScope);
                            }
                            finally
                            {
                                dict.Remove("IoC.Register");
                            }
                        }

                        var pluginsDir = FindOptionValue(args, "--plugins-dir");
                        if (!string.IsNullOrWhiteSpace(pluginsDir))
                        {
                            Ioc.Resolve<ICommand>("Server.Admin.HotReload", pluginsDir).Execute();
                        }

                        router = Ioc.Resolve<IGameRouter>("Server.Router");
                        isIngressPaused = Ioc.Resolve<Func<bool>>("Server.Ingress.Paused");
                        connections = serverConnections;
                        decoder = Ioc.Resolve<ICommandMessageDecoding>("Endpoint.Message.Decoder");
                        options = Ioc.Resolve<ServerOptions>("Server.Options");
                        executeHotReload = dir => new InSeparateScopeCommand(
                            serverScope,
                            new DelegatingCommand(() => Ioc.Resolve<ICommand>("Server.Admin.HotReload", dir).Execute()))
                            .Execute();
                        executeShutdown = snapshotPath => new InSeparateScopeCommand(
                            serverScope,
                            new DelegatingCommand(() => Ioc.Resolve<ICommand>("Server.Admin.Shutdown", snapshotPath!).Execute()))
                            .Execute();
                    })).Execute();
            }
            finally
            {
                restoreScope.Execute();
            }

            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.ClearProviders();

            var app = builder.Build();
            try
            {
                app.UseWebSockets();

                app.MapGet("/health", () => Results.Content("{\"status\":\"ok\"}", "application/json"));

                app.Map(
                    "/ws",
                    async context =>
                    {
                        if (!context.WebSockets.IsWebSocketRequest)
                        {
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            return;
                        }

                        var socket = await context.WebSockets.AcceptWebSocketAsync();
                        try
                        {
                            var channel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
                                new BoundedChannelOptions(options.OutboundChannelCapacity)
                                {
                                    SingleReader = true,
                                    SingleWriter = false,
                                    FullMode = BoundedChannelFullMode.DropOldest,
                                });
                            var sender = new ChannelClientMessageSending(channel);
                            var gameId = context.Request.Query["gameId"].ToString();
                            var connectionId = Guid.NewGuid().ToString("N");
                            var hasGameId = !string.IsNullOrWhiteSpace(gameId);

                            if (hasGameId)
                            {
                                connections.Subscribe(gameId, connectionId, sender);
                            }

                            var loop = new WebSocketMessageLoop(
                                router,
                                isIngressPaused,
                                decoder,
                                sender,
                                options.MaxWebSocketMessageBytes);

                            await loop.RunAsync(
                                socket,
                                () =>
                                {
                                    if (hasGameId)
                                    {
                                        connections.Unsubscribe(gameId, connectionId);
                                    }
                                },
                                context.RequestAborted);
                        }
                        finally
                        {
                            socket.Dispose();
                        }
                    });

                app.MapPost(
                    "/admin/shutdown",
                    (HttpContext context) =>
                    {
                        var rawSnapshotPath = context.Request.Query["snapshotPath"].ToString();
                        var snapshotPath = string.IsNullOrWhiteSpace(rawSnapshotPath) ? null : rawSnapshotPath;
                        executeShutdown(snapshotPath);
                        app.Lifetime.StopApplication();
                        return Results.Content("{\"status\":\"shutting_down\"}", "application/json");
                    });

                app.MapPost(
                    "/admin/hot-reload",
                    (HttpContext context) =>
                    {
                        var pluginsDir = context.Request.Query["pluginsDir"].ToString();
                        if (string.IsNullOrWhiteSpace(pluginsDir))
                        {
                            return Results.BadRequest();
                        }

                        executeHotReload(pluginsDir);
                        return Results.Content("{\"status\":\"reloaded\"}", "application/json");
                    });

                await app.StartAsync(ct);
                if (onStarted != null)
                {
                    onStarted(new Uri(app.Urls.First()));
                }

                await app.WaitForShutdownAsync(ct);
                await app.StopAsync();
            }
            finally
            {
                await app.DisposeAsync();
            }
        }

        private static string? FindOptionValue(string[] args, string optionName)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], optionName, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }
    }
}
