using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PenaltiesController : BaseApiController
    {
        public PenaltiesController(IMediator mediator) : base(mediator)
        {
        }

        [HttpGet("rules")]
                public async Task<IActionResult> GetRules()
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(PenaltiesControllerLogic), nameof(GetRules), Array.Empty<object?>(), ControllerContext));
                }

        [HttpPost("rules")]
                public async Task<IActionResult> CreateRule([FromBody] PenaltiesControllerLogic.PenaltyRuleDto dto)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(PenaltiesControllerLogic), nameof(CreateRule), new object?[] { dto }, ControllerContext));
                }

        [HttpPut("rules/{id}")]
                public async Task<IActionResult> UpdateRule(int id, [FromBody] PenaltiesControllerLogic.PenaltyRuleDto dto)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(PenaltiesControllerLogic), nameof(UpdateRule), new object?[] { id, dto }, ControllerContext));
                }

        [HttpDelete("rules/{id}")]
                public async Task<IActionResult> DeleteRule(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(PenaltiesControllerLogic), nameof(DeleteRule), new object?[] { id }, ControllerContext));
                }

        [HttpGet]
                public async Task<IActionResult> GetPenalties(int? classId, string? date)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(PenaltiesControllerLogic), nameof(GetPenalties), new object?[] { classId, date }, ControllerContext));
                }

        [HttpPost]
                public async Task<IActionResult> CreatePenalty([FromBody] PenaltiesControllerLogic.StudentPenaltyDto dto)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(PenaltiesControllerLogic), nameof(CreatePenalty), new object?[] { dto }, ControllerContext));
                }

        [HttpDelete("{id}")]
                public async Task<IActionResult> DeletePenalty(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(PenaltiesControllerLogic), nameof(DeletePenalty), new object?[] { id }, ControllerContext));
                }

        [HttpGet("summary")]
                public async Task<IActionResult> GetSummary(int semesterId, string mode = "week", string? from = null, string? to = null)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(PenaltiesControllerLogic), nameof(GetSummary), new object?[] { semesterId, mode, from, to }, ControllerContext));
                }
    }
}
