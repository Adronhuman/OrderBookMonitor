﻿using Core.Shared;
using Microsoft.AspNetCore.SignalR;
using OrderBookMonitorBackend.Hubs;
using OrderBookMonitorBackend.Impl.Providers;
using OrderBookMonitorBackend.Impl.Providers.Binance;
using OrderBookMonitorBackend.Interfaces;
using System.Diagnostics;
using static Core.Shared.Constants;

namespace OrderBookMonitorBackend.Workers
{
    public class BinanceWorker : BackgroundService
    {
        private readonly ApiClient _apiClient;
        private readonly IHubContext<OrderBookHub> _hubContext;
        private readonly IOrderBookLogger _logger;
        private readonly MultiplePriceLevelsOrderBookProvider _mplorderBookProvider;
        private readonly List<CancelAndRestartTask> _tasks = [];

        public BinanceWorker(ApiClient httpClientFactory, IHubContext<OrderBookHub> hubContext,
            IOrderBookLogger logger,
            MultiplePriceLevelsOrderBookProvider mplorderBookProvider)
        {
            _logger = logger;
            _apiClient = httpClientFactory;
            _hubContext = hubContext;
            _logger = logger;
            _mplorderBookProvider = mplorderBookProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var levels = new[] { OrderBookSize.S50, OrderBookSize.S100 };
            foreach (var level in levels)
            {
                var (provider, endpoint) = SetupOrderBookProviderForSize(level,
                    // The largest order book contains all entries found in smaller ones
                    // Therefore, it makes sense to log just this one
                    shouldLog: level == levels.Last());
                _mplorderBookProvider.Configure(level, provider, endpoint);
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private (OrderBookProvider, string) SetupOrderBookProviderForSize(OrderBookSize size, bool shouldLog = false)
        {
            var orderBookProvider = new OrderBookProvider(_apiClient, size);
            var endpoint = string.Format(SignalREndpoints.OrderBookUpdate, (int)size);

            if (shouldLog)
            {
                // a little bit about storing snapshots

                // Lets estimate how much memory can it cost
                // Order: (decimal price, decimal amount) = 16*2 ≈ 32 bytes
                // Snapshot: (Bids: 100 Orders, Asks: 100 Orders) + TimeStamp = 200*32 + 8 ≈ 6.4 Kb
                // IF we are updated once per second:
                // 6.4 Kb * 60 seconds * 60 minutes * 24 hours ≈ 553.6512 Mb (per day)
                orderBookProvider.Subscribe((orderBookDiff, snapshot) =>
                {
                    _logger.LogSnapshot(snapshot);
                });
            }

            orderBookProvider.Subscribe((orderBookDiff, snapshot) =>
            {
                _hubContext.Clients.All.SendAsync(endpoint, orderBookDiff);
            });


            _tasks.Add(
                new CancelAndRestartTask((cancellationToken) =>
                {
                    var _ = orderBookProvider.RefreshAndListenChanges(cancellationToken);
                },
                TimeSpan.FromSeconds(10)
            ));

            return (orderBookProvider, endpoint);
        }
    }

    public class CancelAndRestartTask
    {
        private Timer _timer;
        private CancellationTokenSource _cancellation;

        public CancelAndRestartTask(Action<CancellationToken> fnc, TimeSpan period)
        {
            _cancellation = new CancellationTokenSource();
            _timer = new Timer((state) =>
            {
                try
                {
                    _cancellation.Cancel();
                    _cancellation = new CancellationTokenSource();
                    fnc(_cancellation.Token);
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Error in CancelAndRestartTask: {ex.Message}");
                }
            }, null, 0, 6000 * 1000);
        }
    }
}
