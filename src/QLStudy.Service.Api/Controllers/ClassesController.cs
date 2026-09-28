using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClassesController : BaseApiController
    {
        public ClassesController(IMediator mediator) : base(mediator)
        {
        }

        [HttpGet]
                public async Task<ActionResult<IEnumerable<Class>>> GetClasses(int? semesterId)
                {
                    var result = await _mediator.Send(new LegacyControllerActionRequest(nameof(ClassesControllerLogic), nameof(GetClasses), new object?[] { semesterId }, ControllerContext));
                    return result is ActionResult actionResult ? actionResult : new ObjectResult(result);
                }

        [HttpGet("{id}")]
                public async Task<ActionResult<Class>> GetClass(int id)
                {
                    var result = await _mediator.Send(new LegacyControllerActionRequest(nameof(ClassesControllerLogic), nameof(GetClass), new object?[] { id }, ControllerContext));
                    return result is ActionResult actionResult ? actionResult : new ObjectResult(result);
                }

        [HttpPost]
                public async Task<ActionResult<Class>> CreateClass(Class cls)
                {
                    var result = await _mediator.Send(new LegacyControllerActionRequest(nameof(ClassesControllerLogic), nameof(CreateClass), new object?[] { cls }, ControllerContext));
                    return result is ActionResult actionResult ? actionResult : new ObjectResult(result);
                }

        [HttpPut("{id}")]
                public async Task<IActionResult> UpdateClass(int id, Class cls)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(ClassesControllerLogic), nameof(UpdateClass), new object?[] { id, cls }, ControllerContext));
                }

        [HttpDelete("{id}")]
                public async Task<IActionResult> DeleteClass(int id)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(ClassesControllerLogic), nameof(DeleteClass), new object?[] { id }, ControllerContext));
                }

        [HttpPost("{classId}/schedules")]
                public async Task<IActionResult> SetSchedules(int classId, [FromBody] List<ClassSchedule> newSchedules)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(ClassesControllerLogic), nameof(SetSchedules), new object?[] { classId, newSchedules }, ControllerContext));
                }
    }
}
