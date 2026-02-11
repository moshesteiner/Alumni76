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

            var existingUsersMap = await _dbContext.Users
                .ToDictionaryAsync(u => u.Email!, u => u);
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
                            var rowData = ReadRowData(worksheet, row);
                            if (rowData == null)
                            {
                                notAddedUsers.Add($"שורה {row}: חסר פרט חובה (שם, דוא\"ל)");
                                continue;
                            }

                            var (success, logEntry, newPassword) = await ProcessSingleUserRowAsync(rowData, existingUsersMap, notAddedUsers);

                            if (success)
                            {
                                bulkUsersForLog.Add(logEntry);
                                if (!string.IsNullOrEmpty(newPassword))
                                {
                                    successfullyAddedUsers.Add((rowData.FirstName, rowData.Email, newPassword));
                                }
                            }
                        }
                    }
                }

                // --- Final Notification ---
                await SendEmailToLoggedUser(successfullyAddedUsers, notAddedUsers);

                // --- Finalization ---
                var logMessage = $"הוספה גורפת הושלמה. סה\"כ הוספו/שוייכו: {bulkUsersForLog.Count} חברים. שורות כשל: {notAddedUsers.Count}.";
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
        private BulkUserRowData? ReadRowData(ExcelWorksheet worksheet, int row)
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
                return null;
            }

            return new BulkUserRowData
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
            };
        }
        private async Task<(bool success, string logEntry, string? newPassword)> ProcessSingleUserRowAsync(
                                BulkUserRowData rowData,
                                Dictionary<string, User> existingUsersMap,
                                List<string> notAddedUsers)
        {
            User userToProcess;
            string generatedPassword = string.Empty;
            bool isNewUser = false;

            if (existingUsersMap.TryGetValue(rowData.Email, out User? existingUser))
            {
                // 2. User exists: Check for existing assignment
                userToProcess = existingUser;
                notAddedUsers.Add($"שורה {rowData.RowNumber} ({rowData.Email}): החבר כבר קיים.");
                return (false, string.Empty, null);
            }
            else   // New user: Create and save to get the Id
            {
                isNewUser = true;

                generatedPassword = GeneratePassword();

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
                    PasswordHash = _passwordHasher.HashPassword(null!, generatedPassword)
                };
                _dbContext.Users.Add(userToProcess);

                if (!string.IsNullOrEmpty(rowData.Arrives))
                {
                    // Check if the user is already marked as participating
                    bool alreadyExists = await _dbContext.Participates
                        .AnyAsync(p => p.UserId == userToProcess.Id);

                    if (!alreadyExists)
                    {
                        var arrive = new Participate
                        {
                            UserId = userToProcess.Id
                        };
                        _dbContext.Participates.Add(arrive);
                    }
                }

                // send welcome email
                try
                {
                    await _emailService.SendWelcomeEmailAsync(userToProcess.Email!, userToProcess.FirstName!, generatedPassword);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send welcome email to new user {userToProcess.Email}.");
                }

                await _dbContext.SaveChangesAsync();

                existingUsersMap.Add(userToProcess.Email!, userToProcess);
            }

            // --- Log Entry Creation ---
            string assignmentStatus = isNewUser ? "נוצר" : "שויך מחדש";
            string logEntry = $"{userToProcess.FirstName} {userToProcess.LastName} ({userToProcess.Email}) ";

            // Return success status, the log entry, and the password (if new user)
            return (true, logEntry, isNewUser ? generatedPassword : null);
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