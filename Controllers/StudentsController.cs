using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : BaseApiController
    {
        public StudentsController(IMediator mediator) : base(mediator)
        {
        }

        [HttpGet]
                public async Task<ActionResult<IEnumerable<StudentsControllerLogic.StudentResponseDto>>> GetStudents(int? classId, int? semesterId)
                {
                    var result = await _mediator.Send(new LegacyControllerActionRequest(nameof(StudentsControllerLogic), nameof(GetStudents), new object?[] { classId, semesterId }, ControllerContext));
                    return result is ActionResult actionResult ? actionResult : new ObjectResult(result);
                }

        [HttpGet("{id}")]
                public async Task<ActionResult<StudentsControllerLogic.StudentResponseDto>> GetStudent(int id)
                {
                    var result = await _mediator.Send(new LegacyControllerActionRequest(nameof(StudentsControllerLogic), nameof(GetStudent), new object?[] { id }, ControllerContext));
                    return result is ActionResult actionResult ? actionResult : new ObjectResult(result);
                }

        [HttpPost]
                public async Task<ActionResult<Student>> CreateStudent([FromBody] StudentsControllerLogic.StudentSaveDto dto)
                {
                    var result = await _mediator.Send(new LegacyControllerActionRequest(nameof(StudentsControllerLogic), nameof(CreateStudent), new object?[] { dto }, ControllerContext));
                    return result is ActionResult actionResult ? actionResult : new ObjectResult(result);
                }

        [HttpPut("{id}")]
                public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentsControllerLogic.StudentSaveDto dto)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(StudentsControllerLogic), nameof(UpdateStudent), new object?[] { id, dto }, ControllerContext));
                }

        [HttpDelete("{id}")]
                public async Task<IActionResult> DeleteStudent(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(StudentsControllerLogic), nameof(DeleteStudent), new object?[] { id }, ControllerContext));
                }
    }
}
