using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentScoresController : BaseApiController
    {
        public StudentScoresController(IMediator mediator) : base(mediator)
        {
        }

        [HttpGet]
                public async Task<IActionResult> GetScores(int studentId)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(StudentScoresControllerLogic), nameof(GetScores), new object?[] { studentId }, ControllerContext));
                }

        [HttpPost]
                public async Task<IActionResult> CreateScore([FromBody] StudentScoresControllerLogic.StudentScoreDto dto)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(StudentScoresControllerLogic), nameof(CreateScore), new object?[] { dto }, ControllerContext));
                }

        [HttpDelete("{id}")]
                public async Task<IActionResult> DeleteScore(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(StudentScoresControllerLogic), nameof(DeleteScore), new object?[] { id }, ControllerContext));
                }
    }
}
