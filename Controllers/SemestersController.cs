using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SemestersController : BaseApiController
    {
        public SemestersController(IMediator mediator) : base(mediator)
        {
        }

        [HttpGet]
                public async Task<ActionResult<IEnumerable<Semester>>> GetSemesters()
                {
                    var result = await _mediator.Send(new LegacyControllerActionRequest(nameof(SemestersControllerLogic), nameof(GetSemesters), Array.Empty<object?>(), ControllerContext));
                    return result is ActionResult actionResult ? actionResult : new ObjectResult(result);
                }

        [HttpGet("{id}/schedule")]
                public async Task<IActionResult> GetSchedule(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(SemestersControllerLogic), nameof(GetSchedule), new object?[] { id }, ControllerContext));
                }

        [HttpGet("{id}/tuition-matrix")]
                public async Task<IActionResult> GetTuitionMatrix(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(SemestersControllerLogic), nameof(GetTuitionMatrix), new object?[] { id }, ControllerContext));
                }
    }
}
