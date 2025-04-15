using Hangfire;
using InternalPortal.API.Filters;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Caching;
using InternalPortal.ApplicationCore.Interfaces.Email;
using InternalPortal.ApplicationCore.Interfaces.MessageSenders;
using InternalPortal.ApplicationCore.Interfaces.Utilities.Jira;
using InternalPortal.ApplicationCore.Models.MessagingModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController(IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IEmailService emailService,
        ILogMessageSender logMessageSender,
        IProjectManagementService projectManagementService) 
        : ControllerBase
    {
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get()
        {
            var data = await unitOfWork.LevelRepository.GetAllAsync();
            return Ok(data.Select(p=> new
            {
                p.Name,
                p.Id
            }).ToList());
        }
        [HttpGet("redis")]
        public IActionResult Redis()
        {
            var result = cacheService.GetFn("Level", 5, () =>
                unitOfWork.LevelRepository.GetAll().Select(p => new
                {
                    p.Name,
                    p.Id
                }).ToList()
            );
            return Ok(result);
        }
        [HttpGet("email")]
        public IActionResult SendEmail()
        {
            var result = emailService.Send("Test subject", "Test body", new List<string> { "tai.vo@nois.vn" });
            return Ok(result);
        }
        [HttpGet("log")]
        public async Task<IActionResult> Log()
        {
            await logMessageSender.WriteSystemLogAsync(new SystemLogModel
            {
                Event = "Test log",
                LogLevel = (int)ELogLevel.Information,
                CreatedOnUtc = DateTime.UtcNow
            });
            return Ok();
        }

        [HttpGet("test-validate-objectId-middleware")]
        [AllowAnonymous]
        public IActionResult TestMiddleware()
        {
            return Ok("Middleware is OK");
        }
        [HttpGet("sync")]
        [AllowAnonymous]
        public async Task<IActionResult> SyncStoreProcedure()
        {
            var errors = new List<string>();
            var files = Directory.GetFiles("App_Data", "*.sql", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (file.Contains(".sql"))
                {
                    var text = await System.IO.File.ReadAllTextAsync(file);
                    text = text.Replace("{", "{{");
                    text = text.Replace("}", "}}");
                    try
                    {
                        await unitOfWork.Context.Database.ExecuteSqlRawAsync(text);
                    }
                    catch (Exception e)
                    {
                        errors.Add(file);
                        Console.WriteLine(e);
                    }

                }
            }

            return Ok(new
            {
                Message = "Completed!!!",
                Errors = errors
            });
        }

        [HttpGet("sync-issue-type")]
        [AllowAnonymous]
        public IActionResult TriggerTestJiraSync()
        {
            BackgroundJob.Enqueue<IProjectManagementService>(o => o.SyncIssueTypeAsync());
            return Ok("Completed");
        }

        [HttpGet]
        [Route("TestAzure")]
        [TypeFilter(typeof(AzureDevOpsFilter))]
        public async Task<IActionResult> Getdata()
        {
            var email = HttpContext.Items["DevOpEmail"] as string;
            return Ok(email);
        }

        [HttpGet("issue-type")]
        [AllowAnonymous]
        public async Task<IActionResult> SyncIssueType()
        {
            await projectManagementService.SyncIssueTypeInProjectAsync();
            return Ok("Sync Completed");
        }

        [HttpGet("trigger-sync-issue-type")]
        [AllowAnonymous]
        public IActionResult TriggerSyncIssueType(int projectId, DateTime startDate, DateTime endDate)
        {
            BackgroundJob.Enqueue<IJiraService>(o => o.SyncIssueTypeAsync(projectId, startDate, endDate));
            return Ok("Completed");
        }
    }
}
