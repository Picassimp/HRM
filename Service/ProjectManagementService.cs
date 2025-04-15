using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Utilities.AzureDevOps;
using InternalPortal.ApplicationCore.Interfaces.Utilities.Jira;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.AzureDevOps;
using InternalPortal.ApplicationCore.Models.Jira;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class ProjectManagementService : IProjectManagementService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPdfExportService _pdfExportService;
        private readonly IJiraService _jiraService;
        private readonly IAzureDevOpsService _azureDevOpsService;

        public ProjectManagementService(
            IUnitOfWork unitOfWork,
            IPdfExportService pdfExportService,
            IJiraService jiraService,
            IAzureDevOpsService azureDevOpsService
            )
        {
            _unitOfWork = unitOfWork;
            _pdfExportService = pdfExportService;
            _jiraService = jiraService;
            _azureDevOpsService = azureDevOpsService;
        }

        #region Private Methods
        private string FormatWorkingTimeToHourMinSec(decimal workingTime)
        {
            var hour = (int)workingTime / 60;
            var min = workingTime % 60;
            return (hour >= 10 ? hour : "0" + hour) + ":" + (min >= 10 ? min : "0" + min) + ":00";
        }
        private void ManagerExportDetailReporting(Stream stream, List<ProjectDetailForReportingResponse> excelModel)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            using (var xlPackage = new ExcelPackage(stream))
            {
                var ws = xlPackage.Workbook.Worksheets.Add("ManagerDetailReporting");
                var properties = new string[] {
                    "Project",
                    "Company",
                    "User",
                    "Email",
                    "Team",
                    "Task Id",
                    "Description",
                    "Stage",
                    "StartDate",
                    "StartTime",
                    "EndDate",
                    "EndTime",
                    "IssueType",
                    "Tags",
                    "WorkingTime (h)",
                    "WorkingTime (decimal)"
                };
                Dictionary<int, int> listCheckCol = new Dictionary<int, int>();
                for (int i = 0; i < properties.Length; i++)
                {

                    ws.Cells[1, i + 1].Value = properties[i];
                    ws.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(184, 204, 228));
                    ws.Cells[1, i + 1].Style.Font.Bold = true;

                    //add value for list check col: key = column, value = maxLength
                    listCheckCol.Add(i + 1, 0);

                    AutofitComlumnExportExcel(ws, 1, i + 1, listCheckCol);
                }

                ws.View.FreezePanes(2, 1);

                int row = 2;

                foreach (var item in excelModel)
                {
                    int col = 1;
                    ws.Cells[row, col].Value = item.Project;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.Company;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.User;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.Email;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.Team;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.TaskId;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.Description;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.ProjectStageName;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.StartDate.ToString("dd/MM/yyyy");
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.StartTime;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.EndDate.HasValue ? item.EndDate.Value.ToString("dd/MM/yyyy") : null;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.EndTime;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.IssueType;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.Tags;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.WorkingTimeHour;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.WorkingTimeDecimal;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;



                    row++;
                }
                xlPackage.Save();
            }
        }
        private void AutofitComlumnExportExcel(ExcelWorksheet worksheet, int row, int column, Dictionary<int, int> listCheckCol)
        {
            if (!string.IsNullOrEmpty(worksheet.Cells[row, column].GetValue<string>())
                && worksheet.Cells[row, column].GetValue<string>().Length > listCheckCol[column])
            {
                listCheckCol[column] = worksheet.Cells[row, column].GetValue<string>().Length;
                worksheet.Cells[row, column].AutoFitColumns();
            }
        }
        private bool IsValidEmail(string emailaddress)
        {
            try
            {
                MailAddress m = new MailAddress(emailaddress);

                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
        private void AdminExportTimesheet(string groupBy, Stream stream, List<ProjectReportResponse> excelModel)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            var properties = new List<string>();
            var extendProperties = new List<string>() {
                "Thời gian (h)"
            };
            using (var xlPackage = new ExcelPackage(stream))
            {
                var ws = xlPackage.Workbook.Worksheets.Add("ReportTimesheet");
                if (string.IsNullOrEmpty(groupBy))
                {
                    properties = new List<string>() {
                            "Dự án",
                            "Phòng ban",
                            "Công ty",
                            "Nhân viên",
                            "Giai đoạn",
                            "Ngày làm việc",
                            "Loại Task",
                            "Tags",
                            "Thời gian (h)"
                        };
                }
                else
                {
                    var groupItems = groupBy.Split(',').ToList();
                    foreach (var item in groupItems)
                    {
                        switch (item)
                        {
                            case "ProjectName":
                                properties.Add("Dự án");
                                break;
                            case "TeamName":
                                properties.Add("Phòng ban");
                                break;
                            case "Company":
                                properties.Add("Công ty");
                                break;
                            case "UserName":
                                properties.Add("Nhân viên");
                                break;
                            case "ProjectStageName":
                                properties.Add("Giai đoạn");
                                break;
                            case "CreatedDate":
                                properties.Add("Ngày làm việc");
                                break;
                            case "IssueType":
                                properties.Add("Loại Task");
                                break;
                            case "Tags":
                                properties.Add("Tags");
                                break;
                        }
                    }
                }

                properties.AddRange(extendProperties);

                Dictionary<int, int> listCheckCol = new Dictionary<int, int>();
                for (int i = 0; i < properties.Count(); i++)
                {
                    ws.Cells[1, i + 1].Value = properties[i];
                    ws.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(184, 204, 228));
                    ws.Cells[1, i + 1].Style.Font.Bold = true;

                    //add value for list check col: key = column, value = maxLength
                    listCheckCol.Add(i + 1, 0);

                    AutofitComlumnExportExcel(ws, 1, i + 1, listCheckCol);
                }
                int row = 2;
                foreach (var item in excelModel)
                {
                    int col = 1;
                    foreach (var property in properties)
                    {
                        switch (property)
                        {
                            case "Dự án":
                                ws.Cells[row, col].Value = item.ProjectName;
                                ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                                AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                                col++;
                                break;
                            case "Phòng ban":
                                ws.Cells[row, col].Value = item.TeamName;
                                ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                                AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                                col++;
                                break;
                            case "Công ty":
                                ws.Cells[row, col].Value = item.Company;
                                ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                                AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                                col++;
                                break;
                            case "Nhân viên":
                                ws.Cells[row, col].Value = item.UserName;
                                ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                                AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                                col++;
                                break;
                            case "Giai đoạn":
                                ws.Cells[row, col].Value = item.ProjectStageName;
                                ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                                AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                                col++;
                                break;
                            case "Ngày làm việc":
                                ws.Cells[row, col].Value = item.CreatedDate.ToString("dd/MM/yyyy");
                                ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                                AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                                col++;
                                break;
                            case "Loại Task":
                                ws.Cells[row, col].Value = item.IssueType;
                                ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                                AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                                col++;
                                break;
                            case "Tags":
                                ws.Cells[row, col].Value = item.Tags;
                                ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                                AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                                col++;
                                break;
                        }
                    }
                    ws.Cells[row, col].Value = Convert.ToDouble((item.WorkingTime / 60).ToString("0.00"));
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    row++;
                }
                xlPackage.Save();
            }
        }
        #endregion

        public async Task<List<ChartResponse>> ManagerGetChartDataAsync(int managerId, ManagerProjectChartRequest request)
        {
            string firstPattern = @"\((.*?)\)";
            string secondPattern = @"\s*\(.*?\)\s*";

            var projects = await _unitOfWork.ProjectRepository.GetProjectDataFilterAsync(managerId);
            var projectIds = new List<int>();
            var issueTypes = new List<string>();

            if (!string.IsNullOrEmpty(request.ProjectIds))
            {
                var projectIdsRequest = request.ProjectIds.Split(',').Select(int.Parse).ToList();
                if (projectIdsRequest.Any())
                {
                    foreach (var projectId in projectIdsRequest)
                    {
                        if (!projectIds.Contains(projectId))
                        {
                            projectIds.Add(projectId);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(request.IssueTypes))
            {
                var issueTypeRequests = request.IssueTypes.Split(',').ToList();
                foreach (var issueTypeRequest in issueTypeRequests)
                {
                    var match = Regex.Match(issueTypeRequest, firstPattern);
                    if (match.Success)
                    {
                        var projectName = match.Groups[1].Value;
                        var project = projects.Find(o => o.Name == projectName);
                        if (project != null && !projectIds.Contains(project.Id))
                        {
                            projectIds.Add(project.Id);
                        }
                    }
                    issueTypes.Add(Regex.Replace(issueTypeRequest, secondPattern, "").Trim());
                }
            }

            var tags = new List<string>();
            if (!string.IsNullOrEmpty(request.Tags))
            {
                var tagRequests = request.Tags.Split(',').ToList();
                foreach (var tagRequest in tagRequests)
                {
                    var match = Regex.Match(tagRequest, firstPattern);
                    if (match.Success)
                    {
                        string projectName = match.Groups[1].Value;
                        var project = projects.Find(o => o.Name == projectName);
                        if (project != null && !projectIds.Contains(project.Id))
                        {
                            projectIds.Add(project.Id);
                        }
                    }
                    tags.Add(Regex.Replace(tagRequest, secondPattern, "").Trim());
                }
            }

            var newRequest = new ManagerProjectChartRequest
            {
                ProjectIds = projectIds.JoinComma(true),
                UserIds = request.UserIds,
                CompanyIds = request.CompanyIds,
                GroupBy = request.GroupBy,
                ProjectStageIds = request.ProjectStageIds,
                Tags = tags.JoinComma(true),
                IssueTypes = issueTypes.JoinComma(true),
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };

            var rawData = await _unitOfWork.ProjectRepository.ManagerGetChartDataAsync(managerId, newRequest);
            var listChart = new List<ChartResponse>();

            listChart = rawData.Select(x => new ChartResponse()
            {
                CreatedDate = x.CreatedDate,
                WorkingTime = x.WorkingTime,
            }).ToList();

            if (listChart.Any())
            {
                var listCreateDate = listChart.OrderBy(o => o.CreatedDate).Select(o => o.CreatedDate).Distinct().ToList();
                for (DateTime date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
                {
                    if (!listCreateDate.Contains(date))
                    {
                        listChart.Add(new ChartResponse
                        {
                            CreatedDate = date,
                            WorkingTime = 0,
                        });
                    }
                }
            }
            else
            {
                for (DateTime date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
                {
                    listChart.Add(new ChartResponse
                    {
                        CreatedDate = date,
                        WorkingTime = 0,
                    });
                }
            }

            var response = listChart.OrderBy(o => o.CreatedDate).GroupBy(x => x.CreatedDate).Select(x => new ChartResponse()
            {
                CreatedDate = x.Key,
                WorkingTime = x.Sum(x => x.WorkingTime),
            }).ToList();
            return response;
        }

        public async Task<List<ProjectDetailForReportingResponse>> ManagerGetDetailForReportingAsync(int managerId, ManagerProjectDetailRequest request)
        {
            string firstPattern = @"\((.*?)\)";
            string secondPattern = @"\s*\(.*?\)\s*";

            var projects = await _unitOfWork.ProjectRepository.GetProjectDataFilterAsync(managerId);
            var projectIds = new List<int>();
            var issueTypes = new List<string>();

            if (!string.IsNullOrEmpty(request.ProjectIds))
            {
                var projectIdsRequest = request.ProjectIds.Split(',').Select(int.Parse).ToList();
                if (projectIdsRequest.Any())
                {
                    foreach (var projectId in projectIdsRequest)
                    {
                        if (!projectIds.Contains(projectId))
                        {
                            projectIds.Add(projectId);
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(request.IssueTypes))
            {
                var issueTypeRequests = request.IssueTypes.Split(',').ToList();
                foreach (var issueTypeRequest in issueTypeRequests)
                {
                    var match = Regex.Match(issueTypeRequest, firstPattern);
                    if (match.Success)
                    {
                        var projectName = match.Groups[1].Value;
                        var project = projects.Find(o => o.Name == projectName);
                        if (project != null && !projectIds.Contains(project.Id))
                        {
                            projectIds.Add(project.Id);
                        }
                    }
                    issueTypes.Add(Regex.Replace(issueTypeRequest, secondPattern, "").Trim());
                }
            }

            var tags = new List<string>();
            if (!string.IsNullOrEmpty(request.Tags))
            {
                var tagRequests = request.Tags.Split(',').ToList();
                foreach (var tagRequest in tagRequests)
                {
                    var match = Regex.Match(tagRequest, firstPattern);
                    if (match.Success)
                    {
                        string projectName = match.Groups[1].Value;
                        var project = projects.Find(o => o.Name == projectName);
                        if (project != null && !projectIds.Contains(project.Id))
                        {
                            projectIds.Add(project.Id);
                        }
                    }
                    tags.Add(Regex.Replace(tagRequest, secondPattern, "").Trim());
                }
            }

            var newRequest = new ManagerProjectDetailRequest
            {
                ProjectIds = projectIds.JoinComma(true),
                UserIds = request.UserIds,
                TeamIds = request.TeamIds,
                CompanyIds = request.CompanyIds,
                ProjectStageIds = request.ProjectStageIds,
                Tags = tags.JoinComma(true),
                IssueTypes = issueTypes.JoinComma(true),
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };

            var rawData = await _unitOfWork.ProjectRepository.ManagerGetDataDetailForReportingAsync(managerId, request);
            var listReport = new List<ProjectDetailForReportingResponse>();

            listReport = rawData.Select(x => new ProjectDetailForReportingResponse()
            {
                Project = x.Project,
                Company = x.Company,
                Team = x.Team,
                User = x.User,
                Email = x.Email,
                TaskId = x.TaskId,
                Description = x.Description,
                StartDate = x.StartTime,
                StartTime = x.StartTime.ToString("hh:mm:ss tt"),
                EndDate = x.EndTime.HasValue ? x.EndTime.Value : null,
                EndTime = x.EndTime.HasValue ? x.EndTime.Value.ToString("hh:mm:ss tt") : null,
                WorkingTimeHour = FormatWorkingTimeToHourMinSec(x.WorkingTime),
                WorkingTimeDecimal = Convert.ToDecimal((x.WorkingTime / 60).ToString("0.00")),
                ProjectStageName = x.ProjectStageName,
                IssueType = x.IssueType,
                Tags = x.Tags
            }).ToList();
            return listReport;
        }

        public async Task<List<ProjectReportResponse>> ManagerGetReportDataAsync(int managerId, ManagerProjectReportRequest request)
        {
            string firstPattern = @"\((.*?)\)";
            string secondPattern = @"\s*\(.*?\)\s*";

            var projects = await _unitOfWork.ProjectRepository.GetProjectDataFilterAsync(managerId);
            var projectIds = new List<int>();
            var issueTypes = new List<string>();

            if (!string.IsNullOrEmpty(request.ProjectIds))
            {
                var projectIdsRequest = request.ProjectIds.Split(',').Select(int.Parse).ToList();
                if (projectIdsRequest.Any())
                {
                    foreach (var projectId in projectIdsRequest)
                    {
                        if (!projectIds.Contains(projectId))
                        {
                            projectIds.Add(projectId);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(request.IssueTypes))
            {
                var issueTypeRequests = request.IssueTypes.Split(',').ToList();
                foreach (var issueTypeRequest in issueTypeRequests)
                {
                    var match = Regex.Match(issueTypeRequest, firstPattern);
                    if (match.Success)
                    {
                        var projectName = match.Groups[1].Value;
                        var project = projects.Find(o => o.Name == projectName);
                        if (project != null && !projectIds.Contains(project.Id))
                        {
                            projectIds.Add(project.Id);
                        }
                    }
                    issueTypes.Add(Regex.Replace(issueTypeRequest, secondPattern, "").Trim());
                }
            }

            var tags = new List<string>();
            if (!string.IsNullOrEmpty(request.Tags))
            {
                var tagRequests = request.Tags.Split(',').ToList();
                foreach (var tagRequest in tagRequests)
                {
                    var match = Regex.Match(tagRequest, firstPattern);
                    if (match.Success)
                    {
                        string projectName = match.Groups[1].Value;
                        var project = projects.Find(o => o.Name == projectName);
                        if (project != null && !projectIds.Contains(project.Id))
                        {
                            projectIds.Add(project.Id);
                        }
                    }
                    tags.Add(Regex.Replace(tagRequest, secondPattern, "").Trim());
                }
            }

            var newRequest = new ManagerProjectReportRequest
            {
                ProjectIds = projectIds.JoinComma(true),
                UserIds = request.UserIds,
                CompanyIds = request.CompanyIds,
                GroupBy = request.GroupBy,
                ProjectStageIds = request.ProjectStageIds,
                Tags = tags.JoinComma(true),
                IssueTypes = issueTypes.JoinComma(true),
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };

            var rawData = await _unitOfWork.ProjectRepository.ManagerGetReportDataAsync(managerId, newRequest);
            var listReport = new List<ProjectReportResponse>();

            listReport = rawData.Select(x => new ProjectReportResponse()
            {
                ProjectName = x.ProjectName,
                TeamName = x.TeamName,
                UserName = x.UserName,
                Company = x.Company,
                ProjectStageName = x.ProjectStageName,
                IssueType = x.IssueType,
                Tags = x.Tags,
                WorkingTime = x.WorkingTime,
                CreatedDate = x.CreatedDate,
            }).ToList();
            return listReport;
        }

        public byte[] ManagerExportDetailReporting(List<ProjectDetailForReportingResponse> excelModel)
        {
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                ManagerExportDetailReporting(stream, excelModel);
                bytes = stream.ToArray();
            }
            return bytes;
        }

        public async Task<byte[]> ImportTask(int managerId, Stream stream)
        {
            var log = new List<ExportLogModel>();

            var insertCompany = new List<Client>();
            var listUser = await _unitOfWork.UserInternalRepository.GetListUserAsync();
            var listCompany = await _unitOfWork.ClientRepository.GetListCompanyAsync();
            var listProject = await _unitOfWork.ProjectRepository.GetListProjectAsync();
            var listProjectMember = await _unitOfWork.ProjectMemberRepository.GetListProjectMemberAsync();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var pck = new ExcelPackage(stream))
            {
                ExcelWorksheet ws = pck.Workbook.Worksheets.FirstOrDefault();
                if (ws?.Dimension == null)
                {
                    log.Add(new ExportLogModel
                    {
                        Row = 0,
                        Name = "File error",
                        Description = "File rỗng"
                    });
                    return _pdfExportService.ExportLogPdf(log.OrderBy(x => x.Row).ToList());
                }

                #region Get Title

                var titleProject = ws.Cells[1, 1].GetValue<string>() ?? string.Empty;
                var titleCompany = ws.Cells[1, 2].GetValue<string>() ?? string.Empty;
                var titleDescription = ws.Cells[1, 3].GetValue<string>() ?? string.Empty;
                var titleTask = ws.Cells[1, 4].GetValue<string>() ?? string.Empty;
                var titleUser = ws.Cells[1, 5].GetValue<string>() ?? string.Empty;
                var titleDepartment = ws.Cells[1, 6].GetValue<string>() ?? string.Empty;
                var titleEmail = ws.Cells[1, 7].GetValue<string>() ?? string.Empty;
                var titleTags = ws.Cells[1, 8].GetValue<string>() ?? string.Empty;
                var titleBillable = ws.Cells[1, 9].GetValue<string>() ?? string.Empty;
                var titleStartDate = ws.Cells[1, 10].GetValue<string>() ?? string.Empty;
                var titleStartTime = ws.Cells[1, 11].GetValue<string>() ?? string.Empty;
                var titleEndDate = ws.Cells[1, 12].GetValue<string>() ?? string.Empty;
                var titleEndTime = ws.Cells[1, 13].GetValue<string>() ?? string.Empty;


                if (!titleProject.Equals("Project", StringComparison.InvariantCultureIgnoreCase)
                    || !titleCompany.Equals("Client", StringComparison.InvariantCultureIgnoreCase)
                    || !titleDescription.Equals("Description", StringComparison.InvariantCultureIgnoreCase)
                    || !titleTask.Equals("Task", StringComparison.InvariantCultureIgnoreCase)
                    || !titleUser.Equals("User", StringComparison.InvariantCultureIgnoreCase)
                    || !titleDepartment.Equals("Group", StringComparison.InvariantCultureIgnoreCase)
                    || !titleEmail.Equals("Email", StringComparison.InvariantCultureIgnoreCase)
                    || !titleTags.Equals("Tags", StringComparison.InvariantCultureIgnoreCase)
                    || !titleBillable.Equals("Billable", StringComparison.InvariantCultureIgnoreCase)
                    || !titleStartDate.Equals("Start Date", StringComparison.InvariantCultureIgnoreCase)
                    || !titleStartTime.Equals("Start Time", StringComparison.InvariantCultureIgnoreCase)
                    || !titleEndDate.Equals("End Date", StringComparison.InvariantCultureIgnoreCase)
                    || !titleEndTime.Equals("End Time", StringComparison.InvariantCultureIgnoreCase))
                {
                    log.Add(new ExportLogModel
                    {
                        Row = 0,
                        Name = "File error",
                        Description = "Incorrect file format."
                    });
                    return _pdfExportService.ExportLogPdf(log.OrderBy(x => x.Row).ToList());
                }

                #endregion

                #region Regex
                Regex regexDate = new Regex(@"(((0|1)[0-9]|2[0-9]|3[0-1])\/(0[1-9]|1[0-2])\/((19|20)\d\d))$");
                Regex regexMMDDYYYY = new Regex(@"(0[1-9]|1[0-2])\/(((0|1)[0-9]|2[0-9]|3[0-1])\/((19|20)\d\d))$");
                Regex regexMMDDYYYYHHMMSS = new Regex(@"(0[1-9]|1[0-2])\/(((0|1)[0-9]|2[0-9]|3[0-1])\/((19|20)\d\d)\s+(0[0-9]|1[0-9]|2[0-3])\:(0[0-9]|[1-5][0-9])\:(0[0-9]|[1-5][0-9]))$");
                #endregion

                #region Validate

                var listProjectExcel = new List<ImportTaskExcelModel>();
                for (var row = 2; row <= ws.Dimension.End.Row - ws.Dimension.Start.Row + 1; row++)
                {
                    var project = ws.Cells[row, 1].GetValue<string>() ?? string.Empty;
                    var company = ws.Cells[row, 2].GetValue<string>() ?? string.Empty;
                    var description = ws.Cells[row, 3].GetValue<string>() ?? string.Empty;
                    var task = ws.Cells[row, 4].GetValue<string>() ?? string.Empty;
                    var user = ws.Cells[row, 5].GetValue<string>() ?? string.Empty;
                    var department = ws.Cells[row, 6].GetValue<string>() ?? string.Empty;
                    var email = ws.Cells[row, 7].GetValue<string>() ?? string.Empty;
                    var tags = ws.Cells[row, 8].GetValue<string>() ?? string.Empty;
                    var billable = ws.Cells[row, 9].GetValue<string>() ?? string.Empty;
                    var startDate = ws.Cells[row, 10].GetValue<string>() ?? string.Empty;
                    var startTime = ws.Cells[row, 11].GetValue<string>() ?? string.Empty;
                    var endDate = ws.Cells[row, 12].GetValue<string>() ?? string.Empty;
                    var endTime = ws.Cells[row, 13].GetValue<string>() ?? string.Empty;

                    #region Check NullOrEmpty 
                    if (string.IsNullOrEmpty(company) || string.IsNullOrWhiteSpace(company))
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"Khach hang khong duoc trong"
                        });
                        continue;
                    }
                    if (string.IsNullOrEmpty(project) || string.IsNullOrWhiteSpace(project))
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"Du an khong duoc trong"
                        });
                        continue;
                    }
                    if (string.IsNullOrEmpty(billable) || string.IsNullOrWhiteSpace(billable))
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"Tinh phi khong duoc trong"
                        });
                        continue;
                    }
                    if (string.IsNullOrEmpty(description) || string.IsNullOrWhiteSpace(description))
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"Noi dung task khong duoc trong"
                        });
                        continue;
                    }
                    if (string.IsNullOrEmpty(email))
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"Email khong duoc trong"
                        });
                        continue;
                    }
                    if (!IsValidEmail(email))
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"Email không hop le"
                        });
                        continue;
                    }
                    if (string.IsNullOrEmpty(startTime) || string.IsNullOrWhiteSpace(startTime))
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"Thoi gian bat dau khong duoc trong"
                        });
                        continue;
                    }
                    if (string.IsNullOrEmpty(endTime) || string.IsNullOrWhiteSpace(endTime))
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"thoi gian ket thuc khong duoc trong"
                        });
                        continue;
                    }
                    #endregion

                    #region Validate Start/End - Date/Time
                    DateTime? dtStartDate = null;
                    bool isValidStartDate = regexMMDDYYYY.IsMatch(startDate);
                    if (isValidStartDate)
                    {
                        isValidStartDate = DateTime.TryParseExact(startDate, "MM/dd/yyyy", new CultureInfo("en-GB"), DateTimeStyles.None, out DateTime dt);
                        if (!isValidStartDate)
                        {
                            log.Add(new ExportLogModel
                            {
                                Row = row,
                                Name = string.Empty,
                                Description = $"Ngay bat dau khong hop le"
                            });
                            continue;
                        }
                        dtStartDate = dt;
                    }
                    else
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"Ngay bat dau khong hop le"
                        });
                        continue;
                    }

                    DateTime? dtEndDate = null;
                    bool isValiEndDate = regexMMDDYYYY.IsMatch(endDate);
                    if (isValiEndDate)
                    {
                        isValiEndDate = DateTime.TryParseExact(endDate, "MM/dd/yyyy", new CultureInfo("en-GB"), DateTimeStyles.None, out DateTime dt);
                        if (!isValiEndDate)
                        {
                            log.Add(new ExportLogModel
                            {
                                Row = row,
                                Name = string.Empty,
                                Description = $"Ngay ket thuc khong hop le"
                            });
                            continue;
                        }
                        dtEndDate = dt;
                    }
                    else
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"Ngay ket thuc khong hop le"
                        });
                        continue;
                    }

                    DateTime? dtStartTime = null;
                    var splitStartTime = startTime.Split(" ");
                    TimeOnly tempStartTime = TimeOnly.Parse(splitStartTime[0]);
                    TimeOnly otherTempStartTime = TimeOnly.Parse(splitStartTime[0]);
                    if (splitStartTime[1].ToLower() == "pm")
                    {
                        tempStartTime = tempStartTime.AddHours(12);
                    }
                    string fullStartDateTime = startDate + " " + tempStartTime + ":00";
                    bool isValidStartTime = regexMMDDYYYYHHMMSS.IsMatch(fullStartDateTime);
                    if (isValidStartTime)
                    {
                        isValidStartTime = DateTime.TryParseExact(fullStartDateTime, "MM/dd/yyyy HH:mm:ss", new CultureInfo("en-GB"), DateTimeStyles.None, out DateTime dt);
                        if (!isValidStartTime)
                        {
                            log.Add(new ExportLogModel
                            {
                                Row = row,
                                Name = string.Empty,
                                Description = $"Thoi gian bat dau khong hop le"
                            });
                            continue;
                        }
                        if (splitStartTime[1].ToLower() == "pm" && otherTempStartTime >= new TimeOnly(12, 0, 0))
                        {
                            dtStartTime = dt.AddDays(1);
                        }
                        else
                        {
                            dtStartTime = dt;
                        }
                    }
                    else
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"Thoi gian bat dau khong hop le"
                        });
                        continue;
                    }

                    var splitEndTime = endTime.Split(" ");

                    TimeOnly tempEndTime = TimeOnly.Parse(splitEndTime[0]);
                    TimeOnly otherTempEndTime = TimeOnly.Parse(splitEndTime[0]);
                    if (splitEndTime[1].ToLower() == "pm")
                    {
                        tempEndTime = tempEndTime.AddHours(12);
                    }

                    string fullEndDateTime = endDate + " " + tempEndTime + ":00";
                    DateTime? dtEndTime = null;
                    bool isValidEndTime = regexMMDDYYYYHHMMSS.IsMatch(fullEndDateTime);
                    if (isValidEndTime)
                    {
                        isValidEndTime = DateTime.TryParseExact(fullEndDateTime, "MM/dd/yyyy HH:mm:ss", new CultureInfo("en-GB"), DateTimeStyles.None, out DateTime dt);
                        if (!isValidEndTime)
                        {
                            log.Add(new ExportLogModel
                            {
                                Row = row,
                                Name = string.Empty,
                                Description = $"Thoi gian ket thuc khong hop le"
                            });
                            continue;
                        }

                        if (splitEndTime[1].ToLower() == "pm" && otherTempEndTime >= new TimeOnly(12, 0, 0))
                        {
                            dtEndTime = dt.AddDays(1);
                        }
                        else
                        {
                            dtEndTime = dt;
                        }
                    }
                    else
                    {
                        log.Add(new ExportLogModel
                        {
                            Row = row,
                            Name = string.Empty,
                            Description = $"Thoi gian ket thuc khong hop le"
                        });
                        continue;
                    }
                    #endregion

                    var newExcelModel = new ImportTaskExcelModel
                    {
                        Project = project,
                        Company = company,
                        Description = description,
                        Task = task,
                        User = user,
                        Group = department,
                        Email = email,
                        Tags = tags,
                        StartDate = dtStartDate.Value,
                        StartTime = dtStartTime.Value,
                        EndDate = dtEndDate,
                        EndTime = dtEndTime
                    };
                    listProjectExcel.Add(newExcelModel);

                    log.Add(new ExportLogModel
                    {
                        Row = row,
                        Name = string.Empty,
                        Description = $"Import Success"
                    });
                }

                #endregion

                #region Group Model
                // Group theo company => lấy dc Project của Company
                var groupList = listProjectExcel.GroupBy(o => new { o.Company }).Select(o =>
                {
                    var groupCompany = new ImportTaskExcelGroupCompanyModel();
                    groupCompany.Company = o.Key.Company;

                    // Group theo Company + Project => lấy dc email của Company & Project
                    groupCompany.GroupProjectModels = o.GroupBy(o => new { o.Project }).Select(x =>
                    {
                        var groupProject = new ImportTaskExcelGroupProjectModel();
                        groupProject.Project = x.Key.Project;

                        // Group theo Company + Project + Email => lấy dc task của người đó trong project
                        groupProject.GroupEmailModels = x.GroupBy(x => new { x.Email }).Select(y =>
                        {
                            var groupEmail = new ImportTaskExcelGroupEmailModel();
                            groupEmail.Email = y.Key.Email;
                            groupEmail.TaskExcelModels = y.GroupBy(y => new { y.Task, y.Description }).Select(t =>
                            {
                                var groupTask = new TaskExcelModel();
                                groupTask.Task = t.Key.Task;
                                groupTask.Description = t.Key.Description;
                                groupTask.LogTimeExcelModels = t.Select(p => new LogTimeExcelModel
                                {
                                    StartDate = p.StartDate,
                                    StartTime = p.StartTime,
                                    EndDate = p.EndDate,
                                    EndTime = p.EndTime
                                }).ToList();
                                return groupTask;
                            }).ToList();
                            return groupEmail;
                        }).ToList();
                        return groupProject;
                    }).ToList();
                    return groupCompany;
                }).ToList();
                #endregion

                // Lấy danh sách Company
                foreach (var itemCompany in groupList)
                {
                    var companyExist = listCompany.FirstOrDefault(o => o.Company == itemCompany.Company);

                    // nếu company chưa có
                    if (companyExist == null)
                    {
                        // tạo mới company
                        var newCompany = new Client
                        {
                            Company = itemCompany.Company,
                            CreatedByUserId = managerId,
                            IsActive = true,
                            IsDeleted = false
                        };
                        insertCompany.Add(newCompany);

                        var newInsertProject = new List<Project>();
                        // Lấy danh sách project của company
                        foreach (var itemProject in itemCompany.GroupProjectModels)
                        {
                            // tạo mới project
                            var newProject = new Project
                            {
                                Name = itemProject.Project,
                                Integration = (int)EIntegrationService.None,
                                CreatedByUserId = managerId,
                                IsActive = true,
                                IsDeleted = false
                            };
                            newInsertProject.Add(newProject);

                            // Thêm mới Owner
                            var newInsertProjectMember = new List<ProjectMember>();
                            var newOwnerProjectMember = new ProjectMember
                            {
                                Role = (int)EProjectRole.Owner,
                                UserInternalId = managerId,
                                IsActive = true,
                                IsDeleted = false
                            };
                            newInsertProjectMember.Add(newOwnerProjectMember);

                            var newInsertTask = new List<ProjectTimeSheet>();

                            // Lấy danh sách Email của Project để add vào ProjectMember
                            foreach (var itemProjectMember in itemProject.GroupEmailModels)
                            {
                                var userExist = listUser.FirstOrDefault(o => o.Email == itemProjectMember.Email);
                                if (userExist != null)
                                {
                                    // Thêm thành viên, default là dev và có phí
                                    var newProjectMember = new ProjectMember
                                    {
                                        Role = (int)EProjectRole.Developer,
                                        UserInternalId = userExist.Id,
                                        IsActive = true,
                                        IsDeleted = false
                                    };
                                    newInsertProjectMember.Add(newProjectMember);

                                    var newInsertTaskOfMember = new List<ProjectTimeSheet>();

                                    // Lấy danh sách task của ProjectMember
                                    foreach (var itemTimesheet in itemProjectMember.TaskExcelModels)
                                    {
                                        // Thêm mới task    
                                        var newTask = new ProjectTimeSheet
                                        {
                                            ProjectId = newProjectMember.ProjectId,
                                            ProjectMemberId = newProjectMember.Id,
                                            TaskId = itemTimesheet.Task,
                                            Description = itemTimesheet.Description,
                                            CreatedDate = itemTimesheet.LogTimeExcelModels.FirstOrDefault().StartDate,
                                            ProcessStatus = (int)EProcessStatus.NotStart,
                                            IsImport = true
                                        };
                                        newInsertTask.Add(newTask);
                                        newInsertTaskOfMember.Add(newTask);

                                        var newInsertLogTime = new List<ProjectTimesheetLogTime>();
                                        foreach (var itemLogTime in itemTimesheet.LogTimeExcelModels)
                                        {
                                            // Thêm mới logtime
                                            var newLogTime = new ProjectTimesheetLogTime
                                            {
                                                StartTime = itemLogTime.StartTime,
                                                StopTime = itemLogTime.EndTime,
                                                IsImport = true,
                                                IsBillable = true
                                            };
                                            newInsertLogTime.Add(newLogTime);
                                        }
                                        newTask.ProjectTimesheetLogTimes = newInsertLogTime;
                                    }
                                    newProjectMember.ProjectTimeSheets = newInsertTaskOfMember;
                                }
                            }
                            newProject.ProjectMembers = newInsertProjectMember;
                            newProject.ProjectTimeSheets = newInsertTask;
                        }
                        newCompany.Projects = newInsertProject;
                    }

                    // nếu company đã có
                    else
                    {
                        var listProjectOfCompany = companyExist.Projects.ToList();
                        var newInsertProject = new List<Project>();

                        // Tìm project của company
                        foreach (var itemProject in itemCompany.GroupProjectModels)
                        {
                            // Kiểm tra Company có Project hay chưa
                            var projectExist = listProject.FirstOrDefault(o => o.Name == itemProject.Project);

                            // Project = null thì tạo mới tất cả
                            if (projectExist == null)
                            {
                                // tạo mới project
                                var newProject = new Project
                                {
                                    Name = itemProject.Project,
                                    Integration = (int)EIntegrationService.None,
                                    CreatedByUserId = managerId,
                                    IsActive = true,
                                    IsDeleted = false
                                };
                                newInsertProject.Add(newProject);

                                // Thêm mới Owner
                                var newInsertProjectMember = new List<ProjectMember>();
                                var newOwnerProjectMember = new ProjectMember
                                {
                                    Role = (int)EProjectRole.Owner,
                                    UserInternalId = managerId,
                                    IsActive = true,
                                    IsDeleted = false
                                };
                                newInsertProjectMember.Add(newOwnerProjectMember);

                                var newInsertTask = new List<ProjectTimeSheet>();

                                // Lấy danh sách Email của Project để add vào ProjectMember
                                foreach (var itemProjectMember in itemProject.GroupEmailModels)
                                {
                                    var userExist = listUser.FirstOrDefault(o => o.Email == itemProjectMember.Email);
                                    if (userExist != null)
                                    {
                                        // Thêm thành viên, default là dev
                                        var newProjectMember = new ProjectMember
                                        {
                                            Role = (int)EProjectRole.Developer,
                                            UserInternalId = userExist.Id,
                                            IsActive = true,
                                            IsDeleted = false
                                        };
                                        newInsertProjectMember.Add(newProjectMember);

                                        var newInsertTaskOfMember = new List<ProjectTimeSheet>();

                                        // Lấy danh sách task của ProjectMember
                                        foreach (var itemTimesheet in itemProjectMember.TaskExcelModels)
                                        {
                                            // Thêm mới task    
                                            var newTask = new ProjectTimeSheet
                                            {
                                                ProjectId = newProjectMember.ProjectId,
                                                ProjectMemberId = newProjectMember.Id,
                                                TaskId = itemTimesheet.Task,
                                                Description = itemTimesheet.Description,
                                                CreatedDate = itemTimesheet.LogTimeExcelModels.FirstOrDefault().StartDate,
                                                ProcessStatus = (int)EProcessStatus.NotStart,
                                                IsImport = true
                                            };
                                            newInsertTask.Add(newTask);
                                            newInsertTaskOfMember.Add(newTask);

                                            var newInsertLogTime = new List<ProjectTimesheetLogTime>();
                                            foreach (var itemLogTime in itemTimesheet.LogTimeExcelModels)
                                            {
                                                // Thêm mới logtime
                                                var newLogTime = new ProjectTimesheetLogTime
                                                {
                                                    StartTime = itemLogTime.StartTime,
                                                    StopTime = itemLogTime.EndTime,
                                                    IsImport = true,
                                                    IsBillable = true
                                                };
                                                newInsertLogTime.Add(newLogTime);
                                            }
                                            newTask.ProjectTimesheetLogTimes = newInsertLogTime;
                                        }
                                        newProjectMember.ProjectTimeSheets = newInsertTaskOfMember;
                                    }
                                }
                                newProject.ProjectMembers = newInsertProjectMember;
                                newProject.ProjectTimeSheets = newInsertTask;
                            }

                            // Project != null
                            else
                            {
                                var newInsertTaskOfOldMember = new List<ProjectTimeSheet>();
                                var newInsertProjectMember = new List<ProjectMember>();

                                var listTaskOfProject = projectExist.ProjectTimeSheets.ToList();
                                var listMemberOfProject = projectExist.ProjectMembers.ToList();

                                // Kiểm tra xem ProjectMember đã tồn tại chưa
                                foreach (var itemProjectMember in itemProject.GroupEmailModels)
                                {
                                    var userExist = listUser.FirstOrDefault(o => o.Email == itemProjectMember.Email);
                                    if (userExist != null)
                                    {
                                        var memberExist = listMemberOfProject.FirstOrDefault(o => o.UserInternalId == userExist.Id);

                                        // Nếu ko tồn tại ProjectMember thì thêm mới
                                        if (memberExist == null)
                                        {
                                            // Thêm thành viên, default là dev
                                            var newProjectMember = new ProjectMember
                                            {
                                                Role = (int)EProjectRole.Developer,
                                                UserInternalId = userExist.Id,
                                                IsActive = true,
                                                IsDeleted = false
                                            };
                                            newInsertProjectMember.Add(newProjectMember);

                                            var newInsertTaskOfMember = new List<ProjectTimeSheet>();

                                            // Lấy danh sách task của ProjectMember
                                            foreach (var itemTimesheet in itemProjectMember.TaskExcelModels)
                                            {
                                                // Thêm mới task    
                                                var newTask = new ProjectTimeSheet
                                                {
                                                    ProjectId = newProjectMember.ProjectId,
                                                    ProjectMemberId = newProjectMember.Id,
                                                    TaskId = itemTimesheet.Task,
                                                    Description = itemTimesheet.Description,
                                                    CreatedDate = itemTimesheet.LogTimeExcelModels.FirstOrDefault().StartDate,
                                                    ProcessStatus = (int)EProcessStatus.NotStart,
                                                    IsImport = true
                                                };
                                                listTaskOfProject.Add(newTask);
                                                newInsertTaskOfMember.Add(newTask);

                                                var newInsertLogTime = new List<ProjectTimesheetLogTime>();
                                                foreach (var itemLogTime in itemTimesheet.LogTimeExcelModels)
                                                {
                                                    // Thêm mới logtime
                                                    var newLogTime = new ProjectTimesheetLogTime
                                                    {
                                                        StartTime = itemLogTime.StartTime,
                                                        StopTime = itemLogTime.EndTime,
                                                        IsImport = true,
                                                        IsBillable = true
                                                    };
                                                    newInsertLogTime.Add(newLogTime);
                                                }
                                                newTask.ProjectTimesheetLogTimes = newInsertLogTime;
                                            }
                                            newProjectMember.ProjectTimeSheets = newInsertTaskOfMember;
                                        }

                                        // Đã tồn tại ProjectMember
                                        else
                                        {
                                            var listTaskOfMember = memberExist.ProjectTimeSheets.ToList();

                                            // Kiểm tra member đó đã có task hay chưa
                                            foreach (var itemTimesheet in itemProjectMember.TaskExcelModels)
                                            {
                                                var taskExist = listTaskOfMember.FirstOrDefault(o => o.Description == itemTimesheet.Description && o.TaskId == itemTimesheet.Task && itemTimesheet.LogTimeExcelModels.Any(x => x.StartDate == o.CreatedDate));

                                                // Task = null thì thêm mới
                                                if (taskExist == null)
                                                {
                                                    // Thêm mới task
                                                    var newTask = new ProjectTimeSheet
                                                    {
                                                        TaskId = itemTimesheet.Task,
                                                        Description = itemTimesheet.Description,
                                                        CreatedDate = itemTimesheet.LogTimeExcelModels.FirstOrDefault().StartDate,
                                                        ProcessStatus = (int)EProcessStatus.NotStart,
                                                        IsImport = true
                                                    };
                                                    newInsertTaskOfOldMember.Add(newTask);
                                                    listTaskOfProject.Add(newTask);

                                                    var newInsertLogTime = new List<ProjectTimesheetLogTime>();
                                                    foreach (var itemLogTime in itemTimesheet.LogTimeExcelModels)
                                                    {
                                                        // Thêm mới logtime
                                                        var newLogTime = new ProjectTimesheetLogTime
                                                        {
                                                            StartTime = itemLogTime.StartTime,
                                                            StopTime = itemLogTime.EndTime,
                                                            IsImport = true,
                                                            IsBillable = true
                                                        };
                                                        newInsertLogTime.Add(newLogTime);
                                                    }
                                                    newTask.ProjectTimesheetLogTimes = newInsertLogTime;
                                                }

                                                // Ngược lại check logtime
                                                else
                                                {
                                                    var listLogTimeOfTask = taskExist.ProjectTimesheetLogTimes.ToList();
                                                    var newInsertLogTime = new List<ProjectTimesheetLogTime>();
                                                    foreach (var itemLogTime in itemTimesheet.LogTimeExcelModels)
                                                    {
                                                        var logTimeExist = listLogTimeOfTask.FirstOrDefault(o => o.StartTime == itemLogTime.StartTime && o.StopTime == itemLogTime.EndTime);

                                                        if (logTimeExist == null)
                                                        {
                                                            // Thêm mới logtime
                                                            var newLogTime = new ProjectTimesheetLogTime
                                                            {
                                                                StartTime = itemLogTime.StartTime,
                                                                StopTime = itemLogTime.EndTime,
                                                                IsImport = true,
                                                                IsBillable = true
                                                            };
                                                            newInsertLogTime.Add(newLogTime);
                                                        }
                                                    }

                                                    // Cập nhật task nếu có LogTime mới
                                                    if (newInsertLogTime.Any())
                                                    {
                                                        listLogTimeOfTask.AddRange(newInsertLogTime);
                                                        taskExist.ProjectTimesheetLogTimes = listLogTimeOfTask;

                                                        await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(taskExist);
                                                    }
                                                }
                                            }

                                            // Cập nhật Member nếu có Task mới
                                            if (newInsertTaskOfOldMember.Any())
                                            {
                                                listTaskOfMember.AddRange(newInsertTaskOfOldMember);
                                                memberExist.ProjectTimeSheets = listTaskOfMember;
                                                projectExist.ProjectTimeSheets = listTaskOfProject;

                                                await _unitOfWork.ProjectMemberRepository.UpdateAsync(memberExist);
                                                await _unitOfWork.ProjectRepository.UpdateAsync(projectExist);
                                            }
                                        }
                                    }
                                }

                                // Cập nhật Project nếu có Member mới
                                if (newInsertProjectMember.Any())
                                {
                                    listMemberOfProject.AddRange(newInsertProjectMember);
                                    projectExist.ProjectMembers = listMemberOfProject;
                                    projectExist.ProjectTimeSheets = listTaskOfProject;

                                    await _unitOfWork.ProjectRepository.UpdateAsync(projectExist);
                                }
                            }
                        }

                        // Cập nhật Company nếu có Project mới
                        if (newInsertProject.Any())
                        {
                            listProjectOfCompany.AddRange(newInsertProject);
                            companyExist.Projects = listProjectOfCompany;

                            await _unitOfWork.ClientRepository.UpdateAsync(companyExist);
                        }
                    }
                }
            }

            if (insertCompany.Any())
            {
                await _unitOfWork.ClientRepository.CreateRangeAsync(insertCompany);
            }
            await _unitOfWork.SaveChangesAsync();
            return _pdfExportService.ExportLogPdf(log.OrderBy(x => x.Row).ToList());
        }
        public async Task<List<CommonUserProjectsResponse>> UserGetProjectFilterAsync(int userId)
        {
            var rawData = await _unitOfWork.ProjectRepository.UserGetProjectAsync(userId);
            var listProject = new List<CommonUserProjectsResponse>();

            listProject = rawData.GroupBy(x => new { x.ClientName }).Select(x =>
            {
                var project = new CommonUserProjectsResponse();

                project.ClientName = x.Key.ClientName;
                project.Projects = x.Select(y => new FieldProjects
                {
                    Id = y.Id,
                    Name = y.ProjectName,
                    Integration = y.Integration,
                }).ToList();
                return project;
            }).ToList();
            return listProject;
        }

        public async Task<List<UserProjectTimesheetResponse>> UserGetTimesheetAsync(int userId, ProjectTimesheetUserPagingRequest model)
        {
            var rawData = await _unitOfWork.ProjectTimesheetRepository.UserGetProjectTimesheetPagingAsync(userId, model);
            var listGroup = new List<UserProjectTimesheetResponse>();

            listGroup = rawData.OrderBy(x => x.CreatedDate).GroupBy(x => new { x.CreatedDate }).Select(x =>
            {
                var timesheet = new UserProjectTimesheetResponse();

                timesheet.CreatedDate = x.Key.CreatedDate;
                timesheet.TotalTimePerDay = x.Sum(o => o.WorkingTime);
                timesheet.TaskResponses = x.OrderBy(x => x.Id).Select(y => new TaskResponse
                {
                    Id = y.Id,
                    TaskId = y.TaskId,
                    Description = y.Description,
                    WorkingTime = y.WorkingTime,
                    ProcessStatus = y.ProcessStatus,
                    IssueType = y.IssueType
                }).ToList();
                return timesheet;
            }).ToList();
            return listGroup;
        }

        public async Task<List<UserProjectTimesheetSelfResponse>> UserGetTimesheetSelfAsync(int userId, ProjectTimesheetSelfUserPagingRequest model)
        {
            var rawData = await _unitOfWork.ProjectTimesheetRepository.UserGetProjectTimesheetSelfPagingAsync(userId, model);
            var listGroup = new List<UserProjectTimesheetSelfResponse>();
            var projectTimesheetTags = await _unitOfWork.ProjectTimesheetTagRepository.GetByProjectTimesheetIdsAsync(rawData.Select(o => o.Id).ToList());
            listGroup = rawData.OrderBy(x => x.CreatedDate).GroupBy(x => new { x.CreatedDate}).Select(x =>
            {
                var timesheet = new UserProjectTimesheetSelfResponse
                {
                    CreatedDate = x.Key.CreatedDate,
                    TotalTimePerDay = x.Sum(o => o.WorkingTime),
                    TaskResponseByProjectNames = x.OrderBy(x => x.ProjectName).GroupBy(x => x.ProjectName).Select(y => new TaskResponseByProjectName
                    {
                        ProjectName = y.Key,
                        TotalTime = y.Sum(o => o.WorkingTime),
                        TaskSelfResponses = y.OrderBy(x => x.CreatedDate).Select(y => new TaskSelfResponse
                        {
                            Id = y.Id,
                            ProjectId = y.ProjectId,
                            ProjectName = y.ProjectName,
                            TaskId = y.TaskId,
                            Description = y.Description,
                            IssueType = y.IssueType,
                            WorkingTime = y.WorkingTime,
                            ProcessStatus = y.ProcessStatus,
                            EstimateTimeInSecond = y.EstimateTimeInSecond ?? 0,
                            Tags = projectTimesheetTags.Where(o => o.ProjectTimesheetId == y.Id).Select(x => x.Tag).ToList()
                        }).ToList()
                    }).ToList()
                };
                return timesheet;
            }).ToList();
            return listGroup;
        }

        public async Task<List<ManageProjectTimesheetGroupResponse>> ManagerOrOwnerGetGroupAsync(ProjectTimesheetPagingRequest model)
        {
            var response = await _unitOfWork.ProjectTimesheetRepository.ManagerOrOwnerGetProjectPagingAsync(model);
            var listGroup = new List<ManageProjectTimesheetGroupResponse>();

            listGroup = response.OrderBy(x => x.CreatedDate).GroupBy(x => new { x.CreatedDate }).Select(x =>
            {
                var projectGroup = new ManageProjectTimesheetGroupResponse();

                projectGroup.CreatedDate = x.Key.CreatedDate;
                projectGroup.TotalTimePerDay = x.Sum(o => o.WorkingTime);
                projectGroup.ProjectUserResponses = x.GroupBy(o => new { o.UserId, o.UserName }).Select(o =>
                {
                    var userResponse = new ManageProjectTimesheetUserResponse();

                    userResponse.UserId = o.Key.UserId;
                    userResponse.UserName = o.Key.UserName;
                    userResponse.TotalTimePerDayByUser = o.Sum(y => y.WorkingTime);
                    userResponse.TaskResponses = o.Select(y => new TaskResponse
                    {
                        Id = y.Id,
                        TaskId = y.TaskId,
                        Description = y.Description,
                        WorkingTime = y.WorkingTime,
                        ProcessStatus = y.ProcessStatus
                    }).ToList();
                    return userResponse;
                }).ToList();
                return projectGroup;
            }).ToList();
            return listGroup;
        }

        public byte[] ManagerProjectExportExcel(string ProjectName, List<ManageProjectTimesheetPagingResponse> excelModel)
        {
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                ExportManagerProjectManagement(stream, ProjectName, excelModel);
                bytes = stream.ToArray();
            }
            return bytes;
        }
        private void ExportManagerProjectManagement(Stream stream, string ProjectName, List<ManageProjectTimesheetPagingResponse> excelModel)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            using (var xlPackage = new ExcelPackage(stream))
            {
                var ws = xlPackage.Workbook.Worksheets.Add("ProjectManagement");
                var properties = new string[] {
                    "Ngày làm task",
                    "Tên nhân viên",
                    "Task Id",
                    "Tiêu đề task",
                    "Thời gian làm task"
                };
                Dictionary<int, int> listCheckCol = new Dictionary<int, int>();
                for (int i = 0; i < properties.Length; i++)
                {
                    ws.Cells[1, 1, 1, properties.Length].Merge = true;
                    ws.Cells[1, 1, 1, properties.Length].Style.Font.Bold = true;
                    ws.Cells[1, 1, 1, properties.Length].Style.Font.Size = 18;
                    ws.Cells[1, 1, 1, properties.Length].Value = ProjectName;
                    ws.Cells[1, 1, 1, properties.Length].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[1, 1, 1, properties.Length].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[1, 1, 1, properties.Length].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 217, 102));

                    ws.Cells[3, i + 1].Value = properties[i];
                    ws.Cells[3, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[3, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(184, 204, 228));
                    ws.Cells[3, i + 1].Style.Font.Bold = true;

                    //add value for list check col: key = column, value = maxLength
                    listCheckCol.Add(i + 1, 0);

                    AutofitComlumnExportExcel(ws, 3, i + 1, listCheckCol);
                }

                ws.View.FreezePanes(2, 1);

                int row = 4;

                foreach (var item in excelModel)
                {
                    int col = 1;
                    ws.Cells[row, col].Value = item.CreatedDate.ToString("dd/MM/yyyy");
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.UserName;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.TaskId;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    ws.Cells[row, col].Value = item.Description;
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    var hour = (int)item.WorkingTime / 60;
                    var minute = item.WorkingTime % 60;
                    ws.Cells[row, col].Value = hour + "h" + minute + "m";
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    AutofitComlumnExportExcel(ws, row, col, listCheckCol);
                    col++;

                    row++;
                }
                xlPackage.Save();
            }
        }

        public async Task<CombineResponseModel<TimesheetResponse>> PrepareStartTaskAsync(ProjectTimesheetLogTimeRequest request, int userId)
        {
            var res = new CombineResponseModel<TimesheetResponse>();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(request.ProjectId);
            if (project == null)
            {
                res.ErrorMessage = "Dự án không tồn tại";
                return res;
            }

            var projectStageInProcess = project.ProjectStages.FirstOrDefault(o => o.Status == (int)EProjectStageStatus.InProcess);
            var allTasks = await _unitOfWork.ProjectTimesheetRepository.GetByProjectIdAndUserIdAsync(request.ProjectId, userId);
            var task = await _unitOfWork.ProjectTimesheetRepository.GetByIdAsync(request.ProjectTimesheetId);
            if (task == null)
            {
                res.ErrorMessage = "Task không tồn tại";
                return res;
            }

            var now = DateTime.UtcNow.UTCToIct();
            var formatNow = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
            if (task.CreatedDate != formatNow)
            {
                res.ErrorMessage = "Chỉ được bắt đầu task trong ngày";
                return res;
            }
            var listTaskLogTime = task.ProjectTimesheetLogTimes.ToList();

            if (listTaskLogTime.Any())
            {
                var lastLogTime = listTaskLogTime.Last();

                // Kiểm tra task đó đã bắt đầu hay chưa
                if (lastLogTime.StopTime == null)
                {
                    res.ErrorMessage = "Task đã được bắt đầu";
                    return res;
                }
            }

            // Tìm các task khác của 1 người trong project đó
            foreach (var item in allTasks)
            {
                // Nếu task đó đang chạy thì dừng task và cập nhật giờ làm
                if (item.ProcessStatus == (int)EProcessStatus.Running)
                {
                    var projectTimesheetLogTimes = item.ProjectTimesheetLogTimes.ToList();
                    var lastItemLogtime = projectTimesheetLogTimes.Last();
                    if (lastItemLogtime != null)
                    {
                        if (lastItemLogtime.StopTime == null)
                        {
                            lastItemLogtime.StopTime = DateTime.UtcNow.UTCToIct();
                            await _unitOfWork.ProjectTimesheetLogTimeRepository.UpdateAsync(lastItemLogtime);
                        }
                    }
                    item.ProcessStatus = (int)EProcessStatus.Stop;
                    await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(item);
                }
            }

            // Tạo mới logtime
            var newLogTime = new ProjectTimesheetLogTime
            {
                ProjectTimesheetId = task.Id,
                StartTime = DateTime.UtcNow.UTCToIct(),
                IsBillable = true,
                ProjectStageId = projectStageInProcess?.Id
            };
            var data = new TimesheetResponse
            {
                ProjectTimeSheet = task,
                ProjectTimesheetLogTime = newLogTime
            };
            task.ProcessStatus = (int)EProcessStatus.Running;
            res.Status = true;
            res.Data = data;
            return res;
        }

        public async Task<CombineResponseModel<TimesheetResponse>> PrepareStopTaskAsync(ProjectTimesheetLogTimeRequest request)
        {
            var res = new CombineResponseModel<TimesheetResponse>();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(request.ProjectId);
            if (project == null)
            {
                res.ErrorMessage = "Dự án không tồn tại";
                return res;
            }

            var task = await _unitOfWork.ProjectTimesheetRepository.GetByIdAsync(request.ProjectTimesheetId);
            if (task == null)
            {
                res.ErrorMessage = "Task không tồn tại";
                return res;
            }

            // Lấy ra list LogTime của task
            var listTaskLogTime = task.ProjectTimesheetLogTimes.ToList();
            var runningLog = listTaskLogTime.Any() ? listTaskLogTime.Last() : null;

            // Nếu có logtime
            if (runningLog == null)
            {
                res.ErrorMessage = "LogTime không tồn tại";
                return res;
            }

            if (runningLog.StopTime.HasValue)
            {
                res.ErrorMessage = "Task đã dừng";
                return res;
            }

            // 259: tạo log cho từng ngày nếu stop vượt qua ngày start
            var stopTime = DateTime.UtcNow.UTCToIct();

            // Kiểm tra xem StopTime có qua ngày của StartTime hay không ?
            var isOverStartTime = stopTime.Date > runningLog.StartTime.Date;
            if (isOverStartTime)
            {
                // Nếu StopTime khác ngày với StartTime
                // Thì tạo ra nhiều Timesheet với CreatedDate từ task.CreatedDate đến StopDate
                // Tạo ra LogTime tương ứng cho TimeSheet trên
                // Ví dụ: StartTime = 10/03/2024, StopTime = 15/03/2024
                // => Tạo mới 5 TimeSheet có CreatedDate từ ngày 11/03 => 15/03 vì Ngày 10/03 đã tồn tại với LogTime.StopTime = null
                // => Thêm mới 5 LogTime tương ứng với mỗi Timesheet

                // NOTED: 
                // #1. Cho phép tạo ngày lễ và cuối tuần
                // #2. Cho phép tạo 24h
                // #3. Logtime chỉ được trong ngày CreatedDate (Ví dụ: CreatedDate = 10/03 => 10/03 00:00:00 <= StartTime/StopTime <= 10/03 23:59:59)

                // Cập nhật Stoptime cho task đó 
                var startTimeOfTask = runningLog.StartTime;
                runningLog.StopTime = startTimeOfTask.Date.AddDays(1).AddSeconds(-1); // 23:59:59 của StartTime

                var insertTimeSheet = new List<ProjectTimeSheet>();
                for (var day = startTimeOfTask.Date.AddDays(1); day.Date <= stopTime.Date; day = day.AddDays(1))
                {
                    var newLogTimes = new List<ProjectTimesheetLogTime>();

                    // Ngày LogTime không phải là ngày Stop
                    if (day != stopTime.Date)
                    {
                        var newLogTime = new ProjectTimesheetLogTime()
                        {
                            StartTime = day,
                            StopTime = day.AddDays(1).AddSeconds(-1), // 23:59:59 của Day
                            TimeSpentSeconds = (int)(day.AddDays(1).AddSeconds(-1) - day).TotalSeconds,
                            IsImport = false,
                            IsBillable = runningLog.IsBillable,
                            ProjectStageId = runningLog.ProjectStageId
                        };
                        newLogTimes.Add(newLogTime);
                    }

                    // Ngày logtime cùng ngày stop
                    else
                    {
                        var newLogTime = new ProjectTimesheetLogTime()
                        {
                            StartTime = day,
                            StopTime = stopTime,
                            TimeSpentSeconds = (int)(stopTime - day).TotalSeconds,
                            IsImport = false,
                            IsBillable = runningLog.IsBillable,
                            ProjectStageId = runningLog.ProjectStageId
                        };
                        newLogTimes.Add(newLogTime);
                    }

                    var newTimeSheet = new ProjectTimeSheet()
                    {
                        ProjectId = task.ProjectId,
                        ProjectMemberId = task.ProjectMemberId,
                        TaskId = task.TaskId,
                        Description = task.Description,
                        CreatedDate = day,
                        ProcessStatus = (int)EProcessStatus.Stop,
                        IsImport = false,
                        ProjectTimesheetLogTimes = newLogTimes
                    };
                    insertTimeSheet.Add(newTimeSheet);
                }
                if (insertTimeSheet.Any())
                {
                    await _unitOfWork.ProjectTimesheetRepository.CreateRangeAsync(insertTimeSheet);
                }
            }

            // StartTime & StopTime cùng ngày
            else
            {
                runningLog.StopTime = stopTime;
                runningLog.TimeSpentSeconds = (int)(stopTime - runningLog.StartTime).TotalSeconds;
                await _unitOfWork.ProjectTimesheetLogTimeRepository.UpdateAsync(runningLog);
            }
            task.ProcessStatus = (int)EProcessStatus.Stop;

            var data = new TimesheetResponse
            {
                ProjectTimeSheet = task,
                ProjectTimesheetLogTime = runningLog
            };
            res.Status = true;
            res.Data = data;
            return res;
        }

        public byte[] ManagerGetExportTimesheet(string groupBy, List<ProjectReportResponse> excelModel)
        {
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                AdminExportTimesheet(groupBy, stream, excelModel);
                bytes = stream.ToArray();
            }
            return bytes;
        }

        public async Task<List<SupervisorMemberProjectTimesheetGroupResponse>> SupervisorGetTimesheetPagingAsync(int supervisorId, SupervisorProjectTimesheetPagingRequest request)
        {
            // Tìm danh sách cấp dưới của người hiện tại để lấy dự án và thời gian làm việc của những người đó
            var relationDtoResponses = await _unitOfWork.UserRelationRepository.GetAllRelationDtoModelAsync();
            var memberOfUserResponses = await GetMemberByUserIdAsync(supervisorId, relationDtoResponses);
            var memberIds = memberOfUserResponses.Select(o => o.MemberUserId).ToList();

            if (request.UserId.HasValue)
            {
                memberIds = memberIds.Where(o => o == request.UserId).ToList();
            }

            if (memberIds.Count == 0)
            {
                return new List<SupervisorMemberProjectTimesheetGroupResponse>();
            }

            var response = await _unitOfWork.ProjectTimesheetRepository.SupervisorGetTimesheetPagingAsync(memberIds, request);
            var listGroup = new List<SupervisorMemberProjectTimesheetGroupResponse>();

            listGroup = response.OrderBy(x => x.CreatedDate).GroupBy(x => new { x.CreatedDate }).Select(x =>
            {
                var projectGroup = new SupervisorMemberProjectTimesheetGroupResponse();

                projectGroup.CreatedDate = x.Key.CreatedDate;
                projectGroup.TotalTimePerDay = x.Sum(o => o.WorkingTime);
                projectGroup.SupervisorMemberProjectTimesheetResponses = x.GroupBy(o => new { o.UserId, o.UserName }).Select(o =>
                {
                    var userResponse = new SupervisorMemberProjectTimesheetResponse();

                    userResponse.UserId = o.Key.UserId;
                    userResponse.UserName = o.Key.UserName;
                    userResponse.TotalTimePerDayByUser = o.Sum(y => y.WorkingTime);
                    userResponse.MemberProjectResponses = o.Select(y => new MemberProjectResponse
                    {
                        Id = y.ProjectId,
                        ProjectName = y.ProjectName,
                        WorkingTimePerProject = y.WorkingTime
                    }).ToList();
                    return userResponse;
                }).ToList();
                return projectGroup;
            }).ToList();
            return listGroup;
        }

        public async Task<List<MemberOfUserResponse>> GetMemberByUserIdAsync(int userId, List<MemberOfUserResponse> relations)
        {
            // Tìm danh sách cấp dưới của người hiện tại
            // Ví dụ: membersOfUser có 5 người
            var membersOfUser = relations.Where(o => o.LeadUserId == userId).ToList();
            if (membersOfUser.Any())
            {
                // membersOfUser.ToList() : Collection was modified; enumeration operation may not execute occurs
                // Khi loop qua memberOfUser mà có chỉnh sửa sẽ bị đá ra lỗi trên
                // Thêm .ToList() để fix 

                // Loop qua cấp dưới của người hiện tại, để tìm cấp dưới của những người cấp dưới đó
                foreach (var member in membersOfUser.ToList())
                {
                    // Nếu có thì add vào response
                    var result = await GetMemberByUserIdAsync(member.MemberUserId, relations);
                    if (result != null && result.Any())
                    {
                        membersOfUser.AddRange(result);
                    }
                    // Sau khi loop qua người đầu tiên, ví dụ có 3 người cấp dưới
                    // Lúc này danh sách cấp dưới có 10 + 3 = 13 người
                    // Tiếp tục loop người thứ 2 và thêm 3 ngưới mới add vào
                }
            }
            return membersOfUser.OrderBy(o => o.MemberUserName).ToList();
        }

        public async Task SyncIssueTypeAsync()
        {
            var updateProjectTimesheets = new List<ProjectTimeSheet>();
            // Lấy những project có tích hợp Jira hoặc AzureDevops
            var projects = await _unitOfWork.ProjectRepository.GetForSyncIssueTypeAsync();
            foreach (var project in projects)
            {
                var taskDic = new Dictionary<string, string>();
                // Kiểm tra config
                if (project.Integration == (int)EIntegrationService.Jira && !string.IsNullOrEmpty(project.JiraDomain)
                        && !string.IsNullOrEmpty(project.JiraUser) && !string.IsNullOrEmpty(project.JiraKey))
                {
                    // Lấy projectTimesheet có issueType = null trong dự án đó
                    var projectTimeSheets = await _unitOfWork.ProjectTimesheetRepository.GetListByProjectIdAsync(project.Id);
                    if (projectTimeSheets.Any())
                    {
                        foreach (var projectTimeSheet in projectTimeSheets)
                        {
                            if (!string.IsNullOrEmpty(projectTimeSheet.TaskId))
                            {
                                var issueType = taskDic.FirstOrDefault(o => string.Equals(o.Key, projectTimeSheet.TaskId, StringComparison.OrdinalIgnoreCase)).Value;
                                if (!string.IsNullOrEmpty(issueType))
                                {
                                    projectTimeSheet.IssueType = issueType;
                                    updateProjectTimesheets.Add(projectTimeSheet);
                                }
                                else
                                {
                                    var jiraRequest = new JiraRequest
                                    {
                                        TaskId = projectTimeSheet.TaskId,
                                        JiraDomain = project.JiraDomain,
                                        JiraUser = project.JiraUser,
                                        JiraKey = project.JiraKey
                                    };
                                    var jiraResponse = await _jiraService.GetTaskSummaryByTaskIdAsync(jiraRequest);
                                    if (jiraResponse != null && !string.IsNullOrEmpty(jiraResponse.IssueType)
                                        && !string.Equals(projectTimeSheet.IssueType, jiraResponse.IssueType, StringComparison.OrdinalIgnoreCase))
                                    {
                                        projectTimeSheet.IssueType = jiraResponse.IssueType;
                                        updateProjectTimesheets.Add(projectTimeSheet);
                                        taskDic.Add(projectTimeSheet.TaskId, jiraResponse.IssueType);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (project.Integration == (int)EIntegrationService.AzureDevOps && !string.IsNullOrEmpty(project.AzureDevOpsOrganization)
                        && !string.IsNullOrEmpty(project.AzureDevOpsKey) && !string.IsNullOrEmpty(project.AzureDevOpsProject))
                {
                    var projectTimeSheets = await _unitOfWork.ProjectTimesheetRepository.GetListByProjectIdAsync(project.Id);
                    if (projectTimeSheets.Any())
                    {
                        foreach (var projectTimeSheet in projectTimeSheets)
                        {
                            if (!string.IsNullOrEmpty(projectTimeSheet.TaskId))
                            {
                                var issueType = taskDic.FirstOrDefault(o => string.Equals(o.Key, projectTimeSheet.TaskId, StringComparison.OrdinalIgnoreCase)).Value;
                                if (!string.IsNullOrEmpty(issueType))
                                {
                                    projectTimeSheet.IssueType = issueType;
                                    updateProjectTimesheets.Add(projectTimeSheet);
                                }
                                else
                                {
                                    var azureRequest = new AzureDevOpsTitleRequest
                                    {
                                        TaskId = projectTimeSheet.TaskId,
                                        AzureDevOpsKey = project.AzureDevOpsKey,
                                        AzureDevOpsOrganization = project.AzureDevOpsOrganization,
                                        AzureDevOpsProject = project.AzureDevOpsProject
                                    };
                                    var azureResponse = await _azureDevOpsService.GetAzureSystemTitleByTaskIdAsync(azureRequest);
                                    if (azureResponse?.Fields != null && !string.IsNullOrEmpty(azureResponse.Fields.SystemWorkItemType)
                                        && !string.Equals(projectTimeSheet.IssueType, azureResponse.Fields.SystemWorkItemType, StringComparison.OrdinalIgnoreCase))
                                    {
                                        projectTimeSheet.IssueType = azureResponse.Fields.SystemWorkItemType;
                                        updateProjectTimesheets.Add(projectTimeSheet);
                                        taskDic.Add(projectTimeSheet.TaskId, azureResponse.Fields.SystemWorkItemType);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (updateProjectTimesheets.Any())
            {
                await _unitOfWork.ProjectTimesheetRepository.UpdateRangeAsync(updateProjectTimesheets);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        public async Task SyncIssueTypeInProjectAsync()
        {
            var newIssueTypes = new List<ProjectIssueType>();

            // Lấy dự án đang active
            var projects = await _unitOfWork.ProjectRepository.GetListProjectAsync();
            foreach (var project in projects)
            {
                var newIssueTypeOfProjects = new List<string>();
                var projectTimesheets = project.ProjectTimeSheets.ToList();
                foreach (var projectTimesheet in projectTimesheets)
                {
                    var projectTimesheetLogTimes = projectTimesheet.ProjectTimesheetLogTimes.ToList();

                    // Loop qua issue type của logtime để thêm mới
                    var issueTypeOfLogTimes = projectTimesheetLogTimes.Where(o => !string.IsNullOrEmpty(o.IssueType)).Select(z => z.IssueType).Distinct().ToList();
                    foreach (var issueTypeOfLogTime in issueTypeOfLogTimes)
                    {
                        // Kiểm tra không null hoặc empty & không trùng
                        if (!string.IsNullOrEmpty(issueTypeOfLogTime) && !newIssueTypeOfProjects.Contains(issueTypeOfLogTime))
                        {
                            newIssueTypeOfProjects.Add(issueTypeOfLogTime);
                        }
                    }
                }

                // Loop qua issue type của task để thêm mới
                var issueTypeOfTasks = projectTimesheets.Where(o => !string.IsNullOrEmpty(o.IssueType)).Select(z => z.IssueType).Distinct().ToList();
                foreach (var issueTypeOfTask in issueTypeOfTasks)
                {
                    // Kiểm tra không null hoặc empty & không trùng với issue type của logtime
                    if (!string.IsNullOrEmpty(issueTypeOfTask) && !newIssueTypeOfProjects.Contains(issueTypeOfTask))
                    {
                        newIssueTypeOfProjects.Add(issueTypeOfTask);
                    }
                }

                // Tạo collection để thêm mới
                newIssueTypes.AddRange(newIssueTypeOfProjects.Select(o => new ProjectIssueType
                {
                    IssueType = o,
                    ProjectId = project.Id,
                    CreatedDate = DateTime.UtcNow.UTCToIct()
                }).ToList());
            }

            if (newIssueTypes.Any())
            {
                await _unitOfWork.ProjectIssueTypeRepository.CreateRangeAsync(newIssueTypes);
                await _unitOfWork.SaveChangesAsync();
            }
        }
    }
}
