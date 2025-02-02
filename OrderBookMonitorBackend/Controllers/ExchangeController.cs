using Core.Shared.ApiModels;
using Core.Shared.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using OrderBookMonitorBackend.Impl.Providers;

namespace OrderBookMonitorBackend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ExchangeController : ControllerBase
    {
        private readonly MultiplePriceLevelsOrderBookProvider _mplOrderBookProvider;

        public ExchangeController(MultiplePriceLevelsOrderBookProvider orderBookProvider)
        {
            _mplOrderBookProvider = orderBookProvider;
        }

        [HttpGet("healthcheck")]
        public IActionResult HealthCheck()
        {
            return Ok(new { status = "healthy" });
        }

        [HttpGet("orderBook")]
        public async Task<IActionResult> GetOrderBook(int size)
        {
            var (orderBookProvider, updateEndpoint) = _mplOrderBookProvider.DetermineOrderBookProvider(size);
            var snapshot = await orderBookProvider.GetOrderBookSnapshot();

            return Ok(new OrderBookResponse
            {
                Snapshot = snapshot ?? OrderBookSnapshot.CreateDummy(),
                UpdateEndpoint = updateEndpoint
            });
        }
    }
}
