using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RewardOptionsController : BaseApiController
    {
        public RewardOptionsController(IMediator mediator) : base(mediator)
        {
        }

        [HttpGet]
                public async Task<ActionResult<IEnumerable<RewardOption>>> GetRewardOptions()
                {
                    var result = await _mediator.Send(new LegacyControllerActionRequest(nameof(RewardOptionsControllerLogic), nameof(GetRewardOptions), Array.Empty<object?>(), ControllerContext));
                    return result is ActionResult actionResult ? actionResult : new ObjectResult(result);
                }

        [HttpPost]
                public async Task<ActionResult<RewardOption>> CreateRewardOption([FromBody] RewardOption option)
                {
                    var result = await _mediator.Send(new LegacyControllerActionRequest(nameof(RewardOptionsControllerLogic), nameof(CreateRewardOption), new object?[] { option }, ControllerContext));
                    return result is ActionResult actionResult ? actionResult : new ObjectResult(result);
                }

        [HttpDelete("{id}")]
                public async Task<IActionResult> DeleteRewardOption(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(RewardOptionsControllerLogic), nameof(DeleteRewardOption), new object?[] { id }, ControllerContext));
                }
    }
}
