using Bagrut_Eval.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Bagrut_Eval.Pages.Metrics
{
    public class DownloadMetrics     // From Export Table
    {
        public static IServiceProvider? ServiceProvider { get; set; }
        private static DateTime GetISTTime()
        {
            // 1. Get the Time Provider service (We assert ServiceProvider! is not null here)
            var timeProvider = ServiceProvider!.GetRequiredService<ITimeProvider>();

            // 2. Get the guaranteed UTC time
            DateTime utcTime = timeProvider.Now;

            // 3. Define the Time Zone ID for Israel
            const string windowsTimeZoneId = "Israel Standard Time";
            const string linuxTimeZoneId = "Asia/Jerusalem";

            TimeZoneInfo israelTimeZone;

            // 4. Safely try to find the Israel time zone using the Windows ID first
            if (TimeZoneInfo.TryFindSystemTimeZoneById(windowsTimeZoneId, out israelTimeZone!))
            {
                // Success
            }
            // 5. If that fails, try the common Linux/IANA ID as a fallback
            else if (TimeZoneInfo.TryFindSystemTimeZoneById(linuxTimeZoneId, out israelTimeZone!))
            {
                // Success
            }
            else
            {
                // 6. Final Fallback: If no time zone is found (unlikely on Azure), 
                // assume the standard UTC+2 offset.
                // NOTE: This fallback doesn't account for Daylight Saving Time (IDT) changes.
                return utcTime.AddHours(2);
            }

            // 7. Perform the conversion
            DateTime localISTTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, israelTimeZone);
            return localISTTime;
        }
        public static MemoryStream __GenerateWordDocument(string examTitle, List<Export> exports)
        {
            DateTime localTime = GetISTTime();
            var memoryStream = new MemoryStream();
            using (WordprocessingDocument doc = WordprocessingDocument.Create(memoryStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                Body body = mainPart.Document.Body!;

                // Add the date and the header line as separate paragraphs for better control
                var dateParagraph = new Paragraph(
                    new ParagraphProperties(
                        new BiDi() { Val = true },
                        new Justification() { Val = JustificationValues.Right }), 
                        new Run(new Text(localTime.ToString("dd-MM-yyyy")))
                    );
                body.Append(dateParagraph);

                var headerLineParagraph = new Paragraph(
                    new ParagraphProperties(
                        new BiDi() { Val = true },
                        new Justification() { Val = JustificationValues.Left } 
                    ),
                    new Run(new Text("הפיקוח על הוראת מדעי המחשב"))
                );
                body.Append(headerLineParagraph);

                // Add an empty paragraph for spacing
                body.Append(new Paragraph());

                // Add the main title (still centered)
                var mainTitleParagraph = new Paragraph(
                    new ParagraphProperties(
                        new BiDi() { Val = true },
                        new Justification() { Val = JustificationValues.Center },
                        new ParagraphStyleId() { Val = "Heading1" }
                    ),
                    new Run(new RunProperties(new Bold()), new Text($"תוספת למחוון: {examTitle}"))
                );
                body.Append(mainTitleParagraph);

                // Add the "General Notes" section header and list
                var generalNotes = exports.Where(e => e.Issue!.QuestionNumber == null).ToList();
                if (generalNotes.Any())
                {
                    body.Append(CreateRtlParagraph("הערות כלליות:", bold: true));
                    // All general notes (without a part number) will now be indented
                    InsertBulletedList(body, generalNotes, mainPart, 1);
                }

                
            }

            memoryStream.Position = 0;
            return memoryStream;
        }

        public static MemoryStream GenerateWordDocument(string examTitle, List<Export> exports)
        {
            DateTime localTime = GetISTTime();
            var memoryStream = new MemoryStream();
            using (WordprocessingDocument doc = WordprocessingDocument.Create(memoryStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                Body body = mainPart.Document.Body!;

                // ... (Header and Title code remains the same) ...

                // ----------------------------------------------------
                // GENERAL NOTES (Issue!.QuestionNumber == null)
                // ----------------------------------------------------

                var generalNotes = exports.Where(e => e.Issue!.QuestionNumber == null).ToList();
                if (generalNotes.Any())
                {
                    body.Append(CreateRtlParagraph("הערות כלליות:", bold: true));
                    // נשאר כפי שהיה, כי אלו הערות כלליות
                    InsertBulletedList(body, generalNotes, mainPart, 1);
                }

                // ----------------------------------------------------
                // PROCESS ITEMS GROUPED BY QUESTION NUMBER
                // ----------------------------------------------------

                var groupedByQuestion = exports
                    .Where(e => e.Issue!.QuestionNumber != null)
                    .GroupBy(e => e.Issue!.QuestionNumber)
                    .OrderBy(g => g.Key);

                foreach (var questionGroup in groupedByQuestion)
                {
                    // 1. QUESTION NUMBER TITLE
                    body.Append(CreateRtlParagraph($"שאלה {questionGroup.Key}", bold: true));

                    // 🛑 שינוי: הסרנו את הוספת התיאור הגלובלי לשאלה (כדי לאפשר תיאור פר-פריט) 🛑
                    // var mainQuestionIssue = questionGroup.FirstOrDefault()?.Issue;
                    // ... (removed old logic) ...

                    // ----------------------------------------------------
                    // 2. PROCESS ITEMS WITH NO QUESTION PART (Part == null)
                    // ----------------------------------------------------
                    var noPartItems = questionGroup.Where(e => e.Issue!.Part == null).ToList();

                    // 🛑 החלפת InsertBulletedList בלולאה שמדפיסה תיאור ותבליט עבור כל פריט 🛑
                    foreach (var item in noPartItems)
                    {
                        // Add the Issue Description (unbulleted paragraph)
                        if (item.Issue != null && !string.IsNullOrWhiteSpace(item.Issue.Description))
                        {
                            body.Append(CreateRtlParagraph(item.Issue.Description, indent: true));
                            //InsertIssueDescriptionBullet(body, item.Issue.Description, mainPart);
                        }

                        // Add the Final Answer (bulleted paragraph) - only one item in the list
                        InsertBulletedList(body, new List<Export> { item }, mainPart, 1);
                    }

                    // ----------------------------------------------------
                    // 3. PROCESS ITEMS GROUPED BY QUESTION PART (Part != null)
                    // ----------------------------------------------------
                    var groupedByPart = questionGroup
                        .Where(e => e.Issue!.Part != null)
                        .GroupBy(e => e.Issue!.Part!.QuestionPart)
                        .OrderBy(g => g.Key);

                    foreach (var partGroup in groupedByPart)
                    {
                        if (partGroup.Key != null && partGroup.Key != "")
                        {
                            // QUESTION PART TITLE
                            body.Append(CreateRtlParagraph($"סעיף {partGroup.Key}", indent: true, bold: true));

                            // 🛑 הסרנו את הוספת התיאור הגלובלי לסעיף (כדי לאפשר תיאור פר-פריט) 🛑
                            // var partIssue = partGroup.FirstOrDefault()?.Issue;
                            // ... (removed old logic) ...
                        }

                        // 🛑 החלפת InsertBulletedList בלולאה שמדפיסה תיאור ותבליט עבור כל פריט 🛑
                        foreach (var item in partGroup)
                        {
                            // Add the Issue Description (unbulleted paragraph)
                            if (item.Issue != null && !string.IsNullOrWhiteSpace(item.Issue.Description))
                            {
                                body.Append(CreateRtlParagraph(item.Issue.Description, indent: true));
                                //InsertIssueDescriptionBullet(body, item.Issue.Description, mainPart);
                            }

                            // Add the Final Answer (bulleted paragraph) - only one item in the list
                            InsertBulletedList(body, new List<Export> { item }, mainPart, 2);
                        }
                    }
                }
            }

            memoryStream.Position = 0;
            return memoryStream;
        }

        private static Paragraph CreateRtlParagraph(string text, bool bold = false, bool indent = false)
        {
            var paragraph = new Paragraph();
            var properties = new ParagraphProperties(
                new BiDi() { Val = true },
                new Justification() { Val = JustificationValues.Left }
            );

            // Indent the paragraph if the 'indent' flag is true.
            // This is used for 'סעיף #' lines to outdent them from their bullets.
            if (indent)
            {
                properties.Append(new Indentation() { Left = "360" });
            }

            paragraph.Append(properties);

            var run = bold ? new Run(new RunProperties(new Bold()),new Text(text)): new Run(new Text(text));
            paragraph.Append(run);

            return paragraph;
        }

        private static void InsertBulletedList(Body body, List<Export> items, MainDocumentPart mainPart, int level)
        {
            var numberingPart = mainPart.NumberingDefinitionsPart;
            if (numberingPart == null)
            {
                numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
                var numbering = new Numbering(
                    new AbstractNum(
                        // Level 0 for general answers. Indent of 360.
                        new Level(
                            new NumberingFormat() { Val = NumberFormatValues.Bullet },
                            new LevelText() { Val = "•" },
                            new LevelJustification() { Val = LevelJustificationValues.Left },
                            // 4. Reduce space between bullet and first word.
                            // The Hanging property is reduced from "360" to "240".
                            new ParagraphProperties(new Indentation() { Left = "360", Hanging = "240" })
                        )
                        { LevelIndex = 0 },
                        // Level 1 for part-specific answers. Indent of 720.
                        new Level(
                            new NumberingFormat() { Val = NumberFormatValues.Bullet },
                            new LevelText() { Val = "•" },
                            new LevelJustification() { Val = LevelJustificationValues.Left },
                            // 4. Reduce space between bullet and first word.
                            // The Hanging property is reduced from "360" to "240".
                            new ParagraphProperties(new Indentation() { Left = "720", Hanging = "240" })
                        )
                        { LevelIndex = 1 }
                    )
                    { AbstractNumberId = 1 },
                    new NumberingInstance(new AbstractNumId() { Val = 1 }) { NumberID = 1 });
                numbering.Save(numberingPart);
            }
            int numberingId = 1;

            foreach (var item in items)
            {
                var textContent = string.IsNullOrEmpty(item.Description)
                    ? $"{item.Issue!.FinalAnswer?.Content}"
                    : $"{item.Description}";

                if (!string.IsNullOrEmpty(item.Score) || item.Issue!.FinalAnswer?.Score != null)
                {
                    var score = string.IsNullOrEmpty(item.Score)
                        ? item.Issue!.FinalAnswer!.Score
                        : item.Score;
                    textContent += ".";
                    textContent += $"   להוריד {score}%";
                }

                var bulletParagraph = new Paragraph(
                    new ParagraphProperties(
                        new BiDi() { Val = true },
                        new Justification() { Val = JustificationValues.Left }, 
                        new NumberingProperties(
                            new NumberingLevelReference() { Val = level - 1 },
                            new NumberingId() { Val = numberingId })),
                    new Run(
                        new RunProperties(new NoProof()),
                        new Text(textContent)));

                body.Append(bulletParagraph);
            }
            body.Append(new Paragraph());
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // From Metrics table        

        public static MemoryStream GenerateMetricsWordDocument(string examTitle, List<Metric> metrics)
        {
            DateTime localTime = GetISTTime();
            var memoryStream = new MemoryStream();
            using (WordprocessingDocument doc = WordprocessingDocument.Create(memoryStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                Body body = mainPart.Document.Body!;

                // Add the date and the header line as separate paragraphs for better control
                var dateParagraph = new Paragraph(
                    new ParagraphProperties(
                        new BiDi() { Val = true },
                        new Justification() { Val = JustificationValues.Right }),
                        new Run(new Text(localTime.ToString("dd-MM-yyyy")))
                    );
                body.Append(dateParagraph);

                var headerLineParagraph = new Paragraph(
                    new ParagraphProperties(
                        new BiDi() { Val = true },
                        new Justification() { Val = JustificationValues.Left }
                    ),
                    new Run(new Text("הפיקוח על הוראת מדעי המחשב"))
                );
                body.Append(headerLineParagraph);

                // Add an empty paragraph for spacing
                body.Append(new Paragraph());

                // Add the main title (still centered)
                var mainTitleParagraph = new Paragraph(
                    new ParagraphProperties(
                        new BiDi() { Val = true },
                        new Justification() { Val = JustificationValues.Center },
                        new ParagraphStyleId() { Val = "Heading1" }
                    ),
                    new Run(new RunProperties(new Bold()), new Text($"מחוון: {examTitle}"))
                );
                body.Append(mainTitleParagraph);

                // Group by QuestionNumber
                var groupedByQuestion = metrics
                    .GroupBy(m => m.QuestionNumber)
                    .OrderBy(g => g.Key);

                foreach (var questionGroup in groupedByQuestion)
                {
                    // Check if the group is for the "General" section
                    if (string.IsNullOrEmpty(questionGroup.Key) || questionGroup.Key == "0")
                    {
                        body.Append(CreateRtlParagraph("כללי", bold: true));

                        var numberedItems = questionGroup.Where(m => !m.RuleDescription!.Trim().StartsWith("*") && !m.RuleDescription.Trim().StartsWith("הערה")).ToList();
                        var unNumberedItems = questionGroup.Where(m => m.RuleDescription!.Trim().StartsWith("*") || m.RuleDescription.Trim().StartsWith("הערה")).ToList();

                        if (numberedItems.Any())
                        {
                            InsertMetricsNumberedList(body, numberedItems, mainPart);
                        }

                        foreach (var item in unNumberedItems)
                        {
                            body.Append(CreateRtlParagraph(item.RuleDescription!));
                        }
                    }
                    else
                    {
                        // Handle all other questions (non-general)
                        body.Append(CreateRtlParagraph($"שאלה {questionGroup.Key}", bold: true));

                        var groupedByPart = questionGroup
                            .GroupBy(m => m.Part)
                            .OrderBy(g => g.Key);

                        foreach (var partGroup in groupedByPart)
                        {
                            if (partGroup.Key != null && partGroup.Key != "")
                            {
                                body.Append(CreateRtlParagraph($"סעיף {partGroup.Key}", indent: true, bold: true));
                            }
                            InsertMetricsBulletedList(body, partGroup.ToList(), mainPart, 2);
                        }
                    }
                }
            }

            memoryStream.Position = 0;
            return memoryStream;
        }

        private static void InsertMetricsNumberedList(Body body, List<Metric> items, MainDocumentPart mainPart)
        {
            var numberingPart = mainPart.NumberingDefinitionsPart;
            if (numberingPart == null)
            {
                numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
                var numbering = new Numbering(
                    new AbstractNum(
                        new Level(
                            new StartNumberingValue() { Val = 1 },
                            new NumberingFormat() { Val = NumberFormatValues.Decimal },
                            new LevelText() { Val = "%1." },
                            new LevelJustification() { Val = LevelJustificationValues.Left },
                            new ParagraphProperties(new Indentation() { Left = "720", Hanging = "360" })
                        )
                        { LevelIndex = 0 }
                    )
                    { AbstractNumberId = 2 },
                    new NumberingInstance(new AbstractNumId() { Val = 2 }) { NumberID = 2 });
                numbering.Save(numberingPart);
            }
            int numberingId = 2;


            //---------------------------------------------------------
            foreach (var item in items)
            {
                // Create a new paragraph for each numbered item
                var numberedParagraph = new Paragraph(
                    new ParagraphProperties(
                        new BiDi() { Val = true },
                        new Justification() { Val = JustificationValues.Left },
                        new NumberingProperties(
                            new NumberingLevelReference() { Val = 0 },
                            new NumberingId() { Val = numberingId })));

                // Regex to find and separate the Hebrew text from the LTR characters
                var rtlPart = System.Text.RegularExpressions.Regex.Match(item.RuleDescription!, @"^[\u0590-\u05FF\s].*?(?=[(])|.*").Value.Trim();
                var ltrPart = System.Text.RegularExpressions.Regex.Match(item.RuleDescription!, @"\s*\(.*").Value;

                // Create a run for the Hebrew text (RTL)
                var rtlRun = new Run(new Text(rtlPart));
                rtlRun.RunProperties = new RunProperties(new BiDi() { Val = true });
                numberedParagraph.Append(rtlRun);

                // Create a run for the LTR part (parentheses and numbers)
                if (!string.IsNullOrEmpty(ltrPart))
                {
                    var ltrRun = new Run(new Text(ltrPart));
                    ltrRun.RunProperties = new RunProperties(new BiDi() { Val = false }); // Explicitly set as LTR
                    numberedParagraph.Append(ltrRun);
                }

                body.Append(numberedParagraph);
            }
        }

        // The existing helper method, with a slight adjustment to the score suffix
        private static void InsertMetricsBulletedList(Body body, List<Metric> items, MainDocumentPart mainPart, int level)
        {
            var numberingPart = mainPart.NumberingDefinitionsPart;
            if (numberingPart == null)
            {
                numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
                var numbering = new Numbering(
                    new AbstractNum(
                        new Level(
                            new NumberingFormat() { Val = NumberFormatValues.Bullet },
                            new LevelText() { Val = "•" },
                            new LevelJustification() { Val = LevelJustificationValues.Left },
                            new ParagraphProperties(new Indentation() { Left = "720", Hanging = "240" })
                        )
                        { LevelIndex = 0 }
                    )
                    { AbstractNumberId = 1 },
                    new NumberingInstance(new AbstractNumId() { Val = 1 }) { NumberID = 1 });
                numbering.Save(numberingPart);
            }
            int numberingId = 1;

            foreach (var item in items)
            {
                var textContent = item.RuleDescription;

                // Omit the score suffix, as it's already in the RuleDescription
                // if (item.Score.HasValue)
                // {
                //     textContent += $" - {item.Score}%";
                // }

                var bulletParagraph = new Paragraph(
                    new ParagraphProperties(
                        new BiDi() { Val = true },
                        new Justification() { Val = JustificationValues.Left },
                        new NumberingProperties(
                            new NumberingLevelReference() { Val = level - 1 },
                            new NumberingId() { Val = numberingId })),
                    new Run(
                        new RunProperties(new NoProof()),
                        new Text(textContent!)));

                body.Append(bulletParagraph);
            }
        }
        /******************************************************************************************************************/

        // Add this new helper method to the DownloadMetrics class
        private static void InsertIssueDescriptionBullet(Body body, string description, MainDocumentPart mainPart)
        {
            // 1. Get or create the unique numbering definition for the square bullet
            var numberingId = GetOrCreateSquareNumbering(mainPart);

            var bulletParagraph = new Paragraph(
                new ParagraphProperties(
                    new BiDi() { Val = true },
                    new Justification() { Val = JustificationValues.Left },
                    new NumberingProperties(
                        // Use level 0 (the first level)
                        new NumberingLevelReference() { Val = 0 },
                        // Use the new, unique numbering ID
                        new NumberingId() { Val = numberingId })),
                new Run(
                    new RunProperties(new NoProof()),
                    new Text(description)));

            body.Append(bulletParagraph);
        }

        // Add this helper method to ensure the numbering definition exists
        // ------------------------------------------------------------------
        // NEW HELPER METHOD: Ensures the square numbering definition exists
        // ------------------------------------------------------------------
        private static int GetOrCreateSquareNumbering(MainDocumentPart mainPart)
        {
            Numbering numbering;
            // 🛑 שינוי: שימוש ב-NumberingDefinitionsPart במקום NumberingPart 🛑
            DocumentFormat.OpenXml.Packaging.NumberingDefinitionsPart? numberingPart = mainPart.NumberingDefinitionsPart;

            // אם mainPart.NumberingPart הוא null, ניצור חלק חדש
            if (numberingPart == null)
            {
                // 🛑 שינוי: שימוש ב-NumberingDefinitionsPart 🛑
                numberingPart = mainPart.AddNewPart<DocumentFormat.OpenXml.Packaging.NumberingDefinitionsPart>();
                numbering = new Numbering();

                // ... (הגדרות NumberingInstance 1 ו-2 כפי שהיו) ...

                // Add the default round bullet (AbstractNumId 1) 
                numbering.Append(new AbstractNum(
                    new Level(
                        //new NumberingFormat() { Val = NumberingFormatValues.Bullet },
                        new LevelText() { Val = "•" },
                        new LevelJustification() { Val = LevelJustificationValues.Left },
                        new ParagraphProperties(new Indentation() { Left = "360", Hanging = "180" })
                    )
                    { LevelIndex = 0 },
                    new Level(
                        //new NumberingFormat() { Val = NumberingFormatValues.Bullet },
                        new LevelText() { Val = "•" },
                        new LevelJustification() { Val = LevelJustificationValues.Left },
                        new ParagraphProperties(new Indentation() { Left = "720", Hanging = "240" })
                    )
                    { LevelIndex = 1 })
                { AbstractNumberId = 1 });
                numbering.Append(new NumberingInstance(new AbstractNumId() { Val = 1 }) { NumberID = 1 });


                // Add the SQUARE bullet (AbstractNumId 2)
                numbering.Append(new AbstractNum(
                    new Level(
                        //new NumberingFormat() { Val = NumberingFormatValues.Bullet },
                        new LevelText() { Val = "■" },
                        new LevelJustification() { Val = LevelJustificationValues.Left },
                        new ParagraphProperties(new Indentation() { Left = "360", Hanging = "180" })
                    )
                    { LevelIndex = 0 })
                { AbstractNumberId = 2 });
                numbering.Append(new NumberingInstance(new AbstractNumId() { Val = 2 }) { NumberID = 2 });

                numbering.Save(numberingPart);
            }
            else
            {
                numbering = numberingPart.Numbering!;
                // Check if the Square Numbering Abstract ID (2) exists, if not, append it
                if (numbering.Elements<AbstractNum>().FirstOrDefault(a => a.AbstractNumberId?.Value == 2) == null)
                {
                    numbering.Append(new AbstractNum(
                        new Level(
                            //new NumberingFormat() { Val = NumberingFormatValues.Bullet },
                            new LevelText() { Val = "■" },
                            new LevelJustification() { Val = LevelJustificationValues.Left },
                            new ParagraphProperties(new Indentation() { Left = "360", Hanging = "180" })
                        )
                        { LevelIndex = 0 })
                    { AbstractNumberId = 2 });
                    numbering.Append(new NumberingInstance(new AbstractNumId() { Val = 2 }) { NumberID = 2 });
                    numbering.Save();
                }
            }
            return 2;
        }
    }
}