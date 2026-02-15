// Pages/AddUser.cshtml.cs
using Alumni76.Data;
using Alumni76.Models;
using Alumni76.Pages.Common;
using Alumni76.Utilities;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OfficeOpenXml.Packaging.Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims; // Required for User.FindFirst(ClaimTypes.NameIdentifier)
using System.Text;
using System.Threading.Tasks;

namespace Alumni76.Pages
{
    [Authorize(Roles = "Admin")]
    public class AddUserModel : BasePageModel<AddUserModel>
    {
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;

        [BindProperty]
        public User NewUser { get; set; } = new User();

        [TempData]
        public string Message { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile? BulkAddFile { get; set; }

        private List<(User, string)> NewAddedUsers = new List<(User, string)>();


        public AddUserModel(ApplicationDbContext dbContext, ILogger<AddUserModel> logger, IPasswordHasher<User> passwordHasher,
                                    IEmailService emailService, ITimeProvider timeProvider) : base(dbContext, logger, timeProvider)
        {
            _passwordHasher = passwordHasher;
            _emailService = emailService;
        }

        public new async Task OnGetAsync()
        {
            ModelState.Clear();
            Message = string.Empty;
            await base.OnGetAsync();
        }

        public new async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("PasswordHash");
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Generate a simple temp password since we aren't asking for one
            string tempPassword = /*"TempP@ss" +*/ Guid.NewGuid().ToString().Substring(0, 6);
            NewUser.PasswordHash = _passwordHasher.HashPassword(NewUser, tempPassword);
            NewUser.Phone1 = FormatPhoneNumber(NewUser.Phone1);
            NewUser.Phone2 = FormatPhoneNumber(NewUser.Phone2);
            NewUser.Active = true;

            _dbContext.Users.Add(NewUser);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = $"בוגר {NewUser.FirstName} {NewUser.LastName} נוסף בהצלחה!";
            try
            {
                await _emailService.SendWelcomeEmailAsync(NewUser.Email, NewUser.FirstName, tempPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send welcome email to new user {NewUser.Email}.");
            }

            return RedirectToPage();
        }

        // Empty handler for BulkAdd to prevent errors
        public async Task<IActionResult> OnPostBulkAddAsync()
        {
            await base.OnPostAsync();
            NewAddedUsers = new List<(User, string)>();

            if (BulkAddFile == null || BulkAddFile.Length == 0)
            {
                TempData["ErrorMessage"] = "נא לבחור קובץ אקסל";
                return Page();
            }

            // 1. Initial Setup and Pre-loading
            var successfullyAddedUsers = new List<(string name, string email, string password)>();
            var notAddedUsers = new List<string>();
            var bulkUsersForLog = new List<string>();

            // Pre-load all necessary DB data efficiently           
            
            var existingUsersMap = await _dbContext.Users.ToDictionaryAsync(u => u.Email!, u => u, StringComparer.OrdinalIgnoreCase);
            try
            {
                //  Excel Parsing and Row Processing Loop
                OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("Moshe Steiner");
                using (var stream = new MemoryStream())
                {
                    await BulkAddFile.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets.First();
                        int rowCount = worksheet.Dimension.Rows;

                        for (int row = 2; row <= rowCount; row++)
                        {
                            var (rowData, errMessage) = ReadRowData(worksheet, row);
                            if (rowData == null)
                            {
                                notAddedUsers.Add($"שורה {row}: {errMessage}");
                                continue;
                            }

                            var (success, logEntry, newPassword) = await ProcessSingleUserRowAsync(rowData, existingUsersMap, notAddedUsers);

                            if (success)
                            {
                                bulkUsersForLog.Add(logEntry);
                                if (!string.IsNullOrEmpty(newPassword))
                                {
                                    successfullyAddedUsers.Add(($"{rowData.FirstName} {rowData.LastName}", rowData.Email, newPassword));
                                }
                            }
                        }
                    }
                }
                await _dbContext.SaveChangesAsync();

                //  Notify the manager before all other users. prevent emailService crash
                await SendEmailToLoggedUser(successfullyAddedUsers, notAddedUsers);

                foreach (var newUser in NewAddedUsers)
                {
                    try
                    {
                        await _emailService.SendWelcomeEmailAsync(newUser.Item1.Email!, newUser.Item1.FirstName!, newUser.Item2);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send welcome email to new user {newUser.Item1.Email}.");
                    }
                }


               

                // --- Finalization ---
                var logMessage = $"הוספה גורפת הושלמה. סה\"כ הוספו/שוייכו: {bulkUsersForLog.Count} חברים.   שורות שנכשלו: {notAddedUsers.Count}.";
                TempData["SuccessMessage"] = logMessage;
                TempData["BulkUsers"] = ListToString(successfullyAddedUsers);


                // 3. Finalization and Logging
                //return await LogAndFinalizeBulkOperationAsync(successfullyAddedUsers, notAddedUsers, bulkUsersForLog);
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"שגיאה חריגה בתהליך הוספה גורפת: {ex.Message}";
                _logger.LogError(ex, "Bulk add operation failed with unhandled exception.");
            }

            return RedirectToPage();
        }

        private async Task SendEmailToLoggedUser(List<(string, string, string)> added, List<string> notAdded)
        {
            if (User.Identity!.IsAuthenticated)
            {
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                var userFirstName = User.FindFirst(ClaimTypes.Name)?.Value;
                if (!string.IsNullOrEmpty(userEmail) && !string.IsNullOrEmpty(userFirstName))
                {
                    await _emailService.SendAddBulkEmailAsync(userEmail, userFirstName, ListToString(added), ListToString(notAdded));
                }
            }
        }
        private string ListToString(List<(string name, string email, string password)> list)
        {
            string str = "";
            foreach (var user in list)
            {
                str += $"שם: {user.name}, דוא\"ל: {user.email}, סיסמה: {user.password}<br />";
            }
            return str;
        }
        private string ListToString(List<string> list)
        {
            string str = "";
            foreach (var user in list)
            {
                str += $"{user}<br />";
            }
            return str;
        }

        private class BulkUserRowData
        {
            public int RowNumber { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string MaidenName { get; set; } = string.Empty;
            public string NickName { get; set; } = string.Empty;
            public string Class { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone1 { get; set; } = string.Empty;
            public string Phone2 { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string Arrives { get; set; } = string.Empty;
        }
        private (BulkUserRowData?, string) ReadRowData(ExcelWorksheet worksheet, int row)
        {
            // Read columns (assuming 1-based index)
            string? firstName = worksheet.Cells[row, 1].GetValue<string>()?.Trim();
            string? lastName = worksheet.Cells[row, 2].GetValue<string>()?.Trim();
            string? maidenName = worksheet.Cells[row, 3].GetValue<string>()?.Trim();
            string? nickName = worksheet.Cells[row, 4].GetValue<string>()?.Trim();
            string? className = worksheet.Cells[row, 5].GetValue<string>()?.Trim();
            string? email = worksheet.Cells[row, 6].GetValue<string>()?.Trim();
            string? phone1 = worksheet.Cells[row, 7].GetValue<string>()?.Trim();
            string? phone2 = worksheet.Cells[row, 8].GetValue<string>()?.Trim();
            string? address = worksheet.Cells[row, 9].GetValue<string>()?.Trim();
            string? arrives = worksheet.Cells[row, 10].GetValue<string>()?.Trim();

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
            {
                string msg = "";
                if (string.IsNullOrEmpty(email)) msg += "חסר אימייל. ";
                if (string.IsNullOrEmpty(firstName)) msg += "חסר שם פרטי. ";
                if (string.IsNullOrEmpty(lastName)) msg += "חסר שם משפחה.";
                return (null, msg);
            }

            return (new BulkUserRowData
            {
                RowNumber = row,
                FirstName = firstName!,
                LastName = lastName!,
                MaidenName = maidenName!,
                NickName = nickName!,
                Class = className!,
                Email = email!,
                Phone1 = phone1!,
                Phone2 = phone2!,
                Address = address!,
                Arrives = arrives!
            }, "");
        }
        private async Task<(bool success, string logEntry, string? newPassword)> ProcessSingleUserRowAsync(
                                BulkUserRowData rowData,
                                Dictionary<string, User> existingUsersMap,
                                List<string> notAddedUsers)
        {            
            if (existingUsersMap.TryGetValue(rowData.Email.ToLower(), out User? existingUser))
            {
                notAddedUsers.Add($"שורה {rowData.RowNumber} ({rowData.Email}): החבר כבר קיים.");
                return (false, string.Empty, null);
            }
            User userToProcess;
            string generatedPassword = GeneratePassword();
            bool isNewUser = false;

            isNewUser = true;

            userToProcess = new User
            {
                FirstName = rowData.FirstName,
                LastName = rowData.LastName,
                MaidenName = rowData.MaidenName,
                NickName = rowData.NickName,
                Class = rowData.Class,
                Email = rowData.Email,
                Phone1 = FormatPhoneNumber(rowData.Phone1),
                Phone2 = FormatPhoneNumber(rowData.Phone2),
                Address = rowData.Address,
                Active = true,
                PasswordHash = _passwordHasher.HashPassword(null!, generatedPassword)
            };

            _dbContext.Users.Add(userToProcess);

            if (!string.IsNullOrEmpty(rowData.Arrives))
            {
                _dbContext.Participates.Add(new Participate { User = userToProcess });
            }

            NewAddedUsers.Add((userToProcess, generatedPassword));

            existingUsersMap.Add(userToProcess.Email!, userToProcess);

            // --- Log Entry Creation ---
            string logEntry = $"{userToProcess.FirstName} {userToProcess.LastName} ({userToProcess.Email}) ";

            // Return success status, the log entry, and the password (if new user)
            return (true, logEntry, generatedPassword);
        }
        private string GeneratePassword()
        {
            return /*"TempP@ss" + */Guid.NewGuid().ToString().Substring(0, 6);
        }
        private string FormatPhoneNumber(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

            // 1. Remove all non-digits to analyze the pattern
            string cleaned = new string(phone.Where(char.IsDigit).ToArray());

            // 2. Handle International Israel code (972...) -> Convert to local (0...)
            if (cleaned.StartsWith("972") && cleaned.Length > 10)
            {
                cleaned = "0" + cleaned.Substring(3);
            }

            // 3. Israeli Mobile (10 digits: 05X-XXX-XXXX)
            if (cleaned.Length == 10 && cleaned.StartsWith("05"))
            {
                return $"{cleaned.Substring(0, 3)}-{cleaned.Substring(3, 3)}-{cleaned.Substring(6)}";
            }

            // 4. Israeli Landline (9 digits: 0X-XXX-XXXX)
            if (cleaned.Length == 9 && cleaned.StartsWith("0"))
            {
                return $"{cleaned.Substring(0, 2)}-{cleaned.Substring(2, 3)}-{cleaned.Substring(5)}";
            }

            // 5. US/Canada Format (11 digits starting with 1: 1-XXX-XXX-XXXX)
            if (cleaned.Length == 11 && cleaned.StartsWith("1"))
            {
                return $"{cleaned.Substring(0, 1)}-{cleaned.Substring(1, 3)}-{cleaned.Substring(4, 3)}-{cleaned.Substring(7)}";
            }

            // 6. If it's some other international length, return the cleaned digits 
            // (at least this removes the mess if they typed weirdly)
            return cleaned.Length > 0 ? cleaned : phone;
        }
    }
}