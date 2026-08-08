using MediatR;
using Microsoft.AspNetCore.Mvc;
using QLStudy.Domain.Entities;
using QLStudy.Service.Api.Features.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : BaseApiController
    {
        public AttendanceController(IMediator mediator) : base(mediator)
        {
        }

        [HttpGet]
                public async Task<ActionResult<IEnumerable<AttendanceControllerLogic.AttendanceDto>>> GetAttendance(int classId, string date)
                {
                    var result = await _mediator.Send(new LegacyControllerActionRequest(nameof(AttendanceControllerLogic), nameof(GetAttendance), new object?[] { classId, date }, ControllerContext));
                    return result is ActionResult actionResult ? actionResult : new ObjectResult(result);
                }

        [HttpPost]
                public async Task<IActionResult> SaveAttendance(int classId, string date, [FromBody] List<AttendanceControllerLogic.SaveAttendanceDto> dtos)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(AttendanceControllerLogic), nameof(SaveAttendance), new object?[] { classId, date, dtos }, ControllerContext));
                }

        [HttpGet("history")]
                public async Task<IActionResult> GetHistory(int classId)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(AttendanceControllerLogic), nameof(GetHistory), new object?[] { classId }, ControllerContext));
                }

        [HttpDelete]
                public async Task<IActionResult> DeleteAttendance(int classId, string date)
                {
                    return await _mediator.Send(new LegacyControllerActionRequest(nameof(AttendanceControllerLogic), nameof(DeleteAttendance), new object?[] { classId, date }, ControllerContext));
                }
    }
}
