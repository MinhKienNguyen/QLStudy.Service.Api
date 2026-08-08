using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : BaseApiController
    {
        public ReportsController(IMediator mediator) : base(mediator)
        {
        }

        [HttpGet("semesters-summary")]
                public async Task<IActionResult> GetSemestersSummary()
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(ReportsControllerLogic), nameof(GetSemestersSummary), Array.Empty<object?>(), ControllerContext));
                }

        [HttpGet("monthly-revenue")]
                public async Task<IActionResult> GetMonthlyRevenue([FromQuery] int semesterId, [FromQuery] int? fromPeriodId = null, [FromQuery] int? toPeriodId = null)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(ReportsControllerLogic), nameof(GetMonthlyRevenue), new object?[] { semesterId, fromPeriodId, toPeriodId }, ControllerContext));
                }

        [HttpGet("class-revenue")]
                public async Task<IActionResult> GetClassRevenue([FromQuery] int semesterId, [FromQuery] int? fromPeriodId = null, [FromQuery] int? toPeriodId = null)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(ReportsControllerLogic), nameof(GetClassRevenue), new object?[] { semesterId, fromPeriodId, toPeriodId }, ControllerContext));
                }

        [HttpGet("payment-status")]
                public async Task<IActionResult> GetPaymentStatus([FromQuery] int semesterId, [FromQuery] int periodId)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(ReportsControllerLogic), nameof(GetPaymentStatus), new object?[] { semesterId, periodId }, ControllerContext));
                }
    }
}


