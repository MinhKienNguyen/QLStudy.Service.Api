using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TuitionController : BaseApiController
    {
        public TuitionController(IMediator mediator) : base(mediator)
        {
        }

        [HttpPost("payment")]
                public async Task<IActionResult> SavePayment([FromBody] TuitionControllerLogic.PaymentUpdateDto dto)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(TuitionControllerLogic), nameof(SavePayment), new object?[] { dto }, ControllerContext));
                }

        [HttpPost("adjustment")]
                public async Task<IActionResult> SaveAdjustment([FromBody] TuitionControllerLogic.TuitionAdjustmentDto dto)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(TuitionControllerLogic), nameof(SaveAdjustment), new object?[] { dto }, ControllerContext));
                }

        [HttpDelete("adjustment")]
                public async Task<IActionResult> DeleteAdjustment([FromQuery] int studentId, [FromQuery] int classId, [FromQuery] int periodId)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(TuitionControllerLogic), nameof(DeleteAdjustment), new object?[] { studentId, classId, periodId }, ControllerContext));
                }
    }
}
