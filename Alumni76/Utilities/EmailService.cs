using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OfficeOpenXml.Packaging.Ionic.Zlib;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Alumni76.Utilities
{
    // Ensure the IEmailService interface is fully defined here or referenced correctly.
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string body);
        Task<bool> SendIssueClosedEmailAsync(string toEmail, string firstName, string examTitle, string questionNumber, string questionPart, string descriptin, string finalAnswer, string score);
        Task SendDiscussionEmailAsync(List<string> toEmails, List<string> toFirstNames, string senderFirstName, string examTitle, string questionNumber, string questionPart, string description, string message);
        Task SendWelcomeEmailAsync(string toEmail, string firstName, string password);
        Task ResetPasswordEmailAsync(string toEmail, string firstName, string password);
        Task SendAddBulkEmailAsync(string email, string firstName, string added, string notAdded);
        Task SendNewAssignmentEmailAsync(string email, string firstName, string subject, string role);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailClient _emailClient;
        private readonly string _senderAddress;
        private readonly bool _enableEmailSending;
        private readonly string _templatePath;
        private readonly ILogger<EmailService> _logger;

        // CRITICAL: The constructor MUST ONLY accept Azure and Core services
        public EmailService(EmailClient emailClient, IConfiguration configuration, ILogger<EmailService> logger)
        {
            _emailClient = emailClient;
            _templatePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplates");
            _logger = logger;

            // Read all settings from the AzureCommunicationService section
            _senderAddress = configuration.GetValue<string>("AzureCommunicationService:SenderAddress")!;
            // Ensure this is a boolean value in appsettings.json, not a string
            _enableEmailSending = configuration.GetValue<bool>("AzureCommunicationService:EnableEmailSending");
        }

        // Main SendEmailAsync method using the Azure EmailClient
        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            if (!_enableEmailSending)
            {
                _logger.LogWarning($"Email sending disabled. Skipped email to: {toEmail} with subject: {subject}");
                return false;
            }

            try
            {
                var emailContent = new EmailContent(subject)
                {
                    Html = body,
                    PlainText = body // Using HTML as fallback plain text for simplicity
                };

                var emailRecipients = new EmailRecipients(new List<EmailAddress>
                {
                    new EmailAddress(toEmail)
                });

                var emailMessage = new EmailMessage(
                    senderAddress: _senderAddress,
                    recipients: emailRecipients,
                    content: emailContent);

                //emailMessage.SenderDisplayName = "Bagrut Evaluation System";

                // This is the failing line, but it will now use the clean client
                EmailSendOperation emailSendOperation = await _emailClient.SendAsync(
                    wait: Azure.WaitUntil.Completed,
                    message: emailMessage);

                _logger.LogInformation($"Email sent successfully to {toEmail}. Operation ID: {emailSendOperation.Id}");
                return true;
            }


            // CATCH 1: Specific error for test/bounced emails (Swallow/Handle)
            catch (RequestFailedException ex) when (ex.ErrorCode == "EmailDroppedAllRecipientsSuppressed")
            {
                string warningMessage = $"WARNING: Email skipped for test user '{toEmail}'. Azure suppressed the address due to a previous hard bounce (Error: {ex.ErrorCode}). Application flow continued successfully for testing.";

                _logger.LogWarning(ex, warningMessage);
                // Do NOT re-throw. Exits successfully for testing.
                return false;
            }
            // CATCH 2: Specific error for configuration failure (Critical/Re-throw)
            catch (RequestFailedException ex) when (ex.ErrorCode == "DomainNotLinked")
            {
                // Re-throwing a clear error message that the user can see
                _logger.LogError(ex, $"DomainNotLinked Error. Sender used: {_senderAddress}. Please confirm the domain is linked in Azure.");
                throw;
            }
            // CATCH 3: Generic failure (Critical/Re-throw)
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {toEmail} with subject {subject}.");
                throw;
            }
        }
        /*************************************************************************************************************************/
        public async Task<bool> SendIssueClosedEmailAsync(string toEmail, string firstName, string examTitle, string questionNumber, string questionPart, string descriptin, string finalAnswer, string score)
        {
            var templateFile = Path.Combine(_templatePath, "IssueClosedTemplate.html");
            var body = await File.ReadAllTextAsync(templateFile);

            body = body.Replace("{FirstName}", firstName)
                       .Replace("{ExamTitle}", examTitle)
                       .Replace("{QuestionNumber}", questionNumber)
                       .Replace("{QuestionPart}", questionPart)
                       .Replace("{Description}", descriptin)
                       .Replace("{FinalAnswer}", finalAnswer)
                       .Replace("{Score}", score);

            var subject = "תשובת לשאלה בעניין מחוון הבגרות"; 
            return await SendEmailAsync(toEmail, subject, body);
        }

        // Inside the EmailService class
        public async Task SendDiscussionEmailAsync(List<string> toEmails, List<string> toFirstNames, string senderFirstName, string examTitle, string questionNumber, string questionPart, string description, string message)
        {
            var templateFile = Path.Combine(_templatePath, "DiscussionEmailTemplate.html");
            var baseTemplate = await File.ReadAllTextAsync(templateFile);

            var subject = "דיון בנושא מחוון בגרות"; // "Discussion about Bagrut Guideline"
            var issueDetails = $"בחינה: {examTitle}, שאלה: {questionNumber}{(string.IsNullOrEmpty(questionPart) ? "" : " סעיף: " + questionPart)}";

            for (int i = 0; i < toEmails.Count; i++)
            {
                var personalizedBody = baseTemplate.Replace("{ToFirstName}", toFirstNames[i])
                                                  .Replace("{SenderFirstName}", senderFirstName)
                                                  .Replace("{ExamTitle}", examTitle)
                                                  .Replace("{IssueDetails}", issueDetails)
                                                  .Replace("{QuestionNumber}", questionNumber)
                                                  .Replace("{QuestionPart}", questionPart)
                                                  .Replace("{Description}", description)
                                                  .Replace("{Message}", message);
                await SendEmailAsync(toEmails[i], subject, personalizedBody);
            }
        }
        public async Task SendWelcomeEmailAsync(string toEmail, string firstName, string password)
        {
            var templateFile = Path.Combine(_templatePath, "NewUserWelcomeTemplate.html");
            var baseTemplate = await File.ReadAllTextAsync(templateFile);

            var subject = "ברוכים הבאים לאתר בוגרי מחזור כא";
            var personalizedBody = baseTemplate.Replace("{FirstName}", firstName).Replace("{Password}", password);
            await SendEmailAsync(toEmail, subject, personalizedBody);
        }
        public async Task ResetPasswordEmailAsync(string toEmail, string firstName, string password)
        {
            var templateFile = Path.Combine(_templatePath, "ResetPasswordTemplate.html");
            var baseTemplate = await File.ReadAllTextAsync(templateFile);

            var subject = "איפוס סיסמה";
            var personalizedBody = baseTemplate.Replace("{FirstName}", firstName).Replace("{Password}", password);
            await SendEmailAsync(toEmail, subject, personalizedBody);
        }
        public async Task SendAddBulkEmailAsync(string email, string firstName, string added, string notAdded)
        {
            var templateFile = Path.Combine(_templatePath, "BulkUsersTemplate.html");
            var baseTemplate = await File.ReadAllTextAsync(templateFile);

            var subject = "הוספת בוגרים לקבוצה";
           
            var personalizedBody = baseTemplate.Replace("{FirstName}", firstName)
                                                .Replace("{addedUsers}", added)
                                                .Replace("{notAddedUsers}", notAdded);  

            await SendEmailAsync(email, subject, personalizedBody);
        }
        public async Task SendNewAssignmentEmailAsync(string email, string firstName, string subject, string role)
        {
            var templateFile = Path.Combine(_templatePath, "NewSubjectAssignedTemplate.html");
            var baseTemplate = await File.ReadAllTextAsync(templateFile);

            var subj = "הוספת מקצוע הערכה";

            var personalizedBody = baseTemplate.Replace("{FirstName}", firstName)
                                                .Replace("{Subject}", subject)
                                                .Replace("{Role}", role);

            await SendEmailAsync(email, subj, personalizedBody);
        }
    }
}
