using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Bagrut_Eval.Models; // Add this using directive to access your Metric model

namespace Bagrut_Eval.Utilities
{
    // These nested classes are part of the parser's internal structure.
    // They define the temporary objects used during parsing before converting to Metric entities.
    public class GeneralRule
    {
        public string? Description { get; set; }
        public int Value { get; set; } // Renamed from 'Value' to 'Score' for clarity if used as percentage
        public string? Type { get; set; }
    }

    public class SolutionStep
    {
        public string? Description { get; set; }
        public int Value { get; set; }
        public string? Type { get; set; }
    }

    public class Reduction
    {
        public string? Mistake { get; set; }
        public int Value { get; set; }
        public string? Type { get; set; }
    }

    public class QuestionPart
    {
        public string? PartName { get; set; }
        public int PartScore { get; set; }
        public List<SolutionStep> SolutionSteps { get; set; } = new List<SolutionStep>();
        public List<string> AlternativeSolutions { get; set; } = new List<string>();
        public List<Reduction> Reductions { get; set; } = new List<Reduction>();
    }

    public class ExamQuestion
    {
        public string? QuestionNumber { get; set; }
        public string? QuestionTitle { get; set; }
        public List<QuestionPart> Parts { get; set; } = new List<QuestionPart>();
        public List<SolutionStep> GeneralSolutionSteps { get; set; } = new List<SolutionStep>();
        public List<string> GeneralAlternativeSolutions { get; set; } = new List<string>();
        public List<Reduction> GeneralReductions { get; set; } = new List<Reduction>();
    }

    // The main parser class
    public class ExamMetricsParser
    {
        // These lists will temporarily hold the parsed data
        private List<GeneralRule> GeneralRules { get; set; } = new List<GeneralRule>();
        private List<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();

        // Modified ParseDocx to accept a Stream
        public void ParseDocx(Stream docxStream)
        {
            // Clear previous parsing results if this instance is reused
            GeneralRules.Clear();
            ExamQuestions.Clear();

            using (WordprocessingDocument wordDocument = WordprocessingDocument.Open(docxStream, false)!)
            {
                var body = wordDocument.MainDocumentPart!.Document!.Body;
                bool inGeneralSection = false;
                ExamQuestion currentQuestion = null!;
                QuestionPart currentPart = null!;

                foreach (var paragraph in body!.Elements<Paragraph>())
                {
                    string text = paragraph.InnerText.Trim();

                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    // --- High-Priority Section Headers ---

                    // 1. Identify "כללי" (General Section)
                    if (text.StartsWith("כללי"))
                    {
                        inGeneralSection = true;
                        currentQuestion = null!; // Exit question context
                        continue;
                    }

                    // 2. Handle "Object-Oriented Programming" or "שאלות 11 / 13" as a main question
                    // This is crucial as it's not "שאלה X" but acts as a question header.
                    else if (text.Contains("Object-Oriented Programming") || Regex.IsMatch(text, @"^שאלות\s*(\d+)\s*/\s*(\d+).*"))
                    {
                        inGeneralSection = false;
                        // The regex `^שאלות\s*(\d+)\s*/\s*(\d+).*` captures "11" and "13".
                        // You can combine them or decide which one to store. Based on your CSV, "11 / 13" as a string is ideal.
                        Match q1113Match = Regex.Match(text, @"^שאלות\s*(\d+)\s*/\s*(\d+).*");
                        string qNumString = "11/13"; // Default or try to parse from the text if format varies
                        if (q1113Match.Success)
                        {
                            qNumString = $"{q1113Match.Groups[1].Value}/{q1113Match.Groups[2].Value}";
                        }
                        currentQuestion = new ExamQuestion { QuestionNumber = qNumString, QuestionTitle = text }; // Use the full text as title for this special case
                        ExamQuestions.Add(currentQuestion);
                        currentPart = null!;
                        continue;
                    }

                    // 3. Identify "שאלה X" (Main Question)
                    else if (Regex.IsMatch(text, @"^שאלה\s(\d+)(?:\s*–?\s*(.*))?")) // Make question title optional after number
                    {
                        inGeneralSection = false;
                        var match = Regex.Match(text, @"^שאלה\s(\d+)(?:\s*–?\s*(.*))?");
                        //int qNum = int.Parse(match.Groups[1].Value);
                        string qNum = match.Groups[1].Value;
                        string qTitle = match.Groups.Count > 2 && match.Groups[2].Success ? match.Groups[2].Value.Trim() : "";
                        currentQuestion = new ExamQuestion { QuestionNumber = qNum, QuestionTitle = qTitle };
                        ExamQuestions.Add(currentQuestion);
                        currentPart = null!; // Reset part for new question
                        continue;
                    }

                    // 4. Identify "סעיף X – Y%" (Question Part)
                    // This must come *after* main question detection but *before* general solution steps.
                    if (text.StartsWith("אפשרות"))
                    {
                        bool b1 = Regex.IsMatch(text, @"^אפשרות\s*([א-בגדהוזחט])");
                    }
                    //---------------------------
                    if (currentQuestion != null && Regex.IsMatch(text, @"^סעיף\s([א-ת])\s*–\s*(\d+)%"))
                    {
                        var match = Regex.Match(text, @"^סעיף\s([א-ת])\s*–\s*(\d+)%");
                        currentPart = new QuestionPart
                        {
                            PartName = match.Groups[1].Value,
                            PartScore = int.Parse(match.Groups[2].Value)
                        };
                        currentQuestion.Parts.Add(currentPart);
                        continue;
                    }

                    // 5. Identify "פתרון נוסף" or "אפשרות א/ב" (Alternative Solutions)
                    // These should be captured before attempting to parse as regular steps/reductions.

                    //else if (text.Contains("פתרון נוסף") || Regex.IsMatch(text, @"^אפשרות\s*([א-בגדהוזחט])(?!\s*–)"))
                    else if (text.Contains("פתרון נוסף") || Regex.IsMatch(text, @"^אפשרות\s*([א-בגדהוזחט])"))
                    {
                        // Add to the current part's alternatives if available, otherwise to question's general alternatives
                        if (currentPart != null)
                        {
                            currentPart.AlternativeSolutions.Add(text);
                        }
                        else if (currentQuestion != null)
                        {
                            currentQuestion.GeneralAlternativeSolutions.Add(text);
                        }
                        continue;
                    }

                    // 6. Identify Deduction Headers (e.g., "הורדות לשתי האפשרויות:", "הורדות:")
                    // These are just markers, the actual deductions follow.
                    else if (text.StartsWith("הורדות לשתי האפשרויות:") || text.StartsWith("הורדות:"))
                    {
                        // No action needed other than skipping this line from other parsing logic
                        continue;
                    }


                    // --- Data Extraction within Current Context (General Section, Question, or Part) ---

                    // Parse content within the General Section
                    if (inGeneralSection)
                    {
                        int extractedValue = 0;
                        // Regex to find a percentage number, allowing for words like "עד", "סה"כ", "לכל היותר"
                        Match percentMatch = Regex.Match(text, @"(\d+)\s*%", RegexOptions.RightToLeft); // Search from right for last percentage
                        if (percentMatch.Success)
                        {
                            int.TryParse(percentMatch.Groups[1].Value, out extractedValue);
                        }

                        GeneralRules.Add(new GeneralRule
                        {
                            Description = text.Trim(),
                            Value = extractedValue, // Extracted percentage
                            Type = "GeneralPenalty"
                        });
                        // No 'continue' here as it's the last check in this block,
                        // and a new section might immediately follow.
                    }
                    // Parse content within a Question/Part context
                    else if (currentQuestion != null)
                    {
                        // 7. Parse Reductions: Prioritize specific reduction patterns
                        if (text.Contains("– להוריד") || text.Contains("– לא להוריד נקודות"))
                        {
                            var reductionMatch = Regex.Match(text, @"(.+?)\s*–\s*(?:להוריד\s*(\d+)%|לא להוריד נקודות)");
                            if (reductionMatch.Success)
                            {
                                var reduction = new Reduction
                                {
                                    Mistake = reductionMatch.Groups[1].Value.Trim(),
                                    Value = reductionMatch.Groups[2].Success ? int.Parse(reductionMatch.Groups[2].Value) : 0, // 0 for "לא להוריד נקודות"
                                    Type = "Penalty"
                                };

                                if (currentPart != null)
                                {
                                    currentPart.Reductions.Add(reduction);
                                }
                                else
                                {
                                    currentQuestion.GeneralReductions.Add(reduction);
                                }
                                continue; // Processed, move to next paragraph
                            }
                        }
                        // 8. Parse Solution Steps: These are lines describing a scoring item.
                        // Needs to be robust for various formats like "כותרת – 3%", "פעולה – 3%".
                        else if (Regex.IsMatch(text, @"^(.+?)\s*–\s*(\d+)%")) // Simple X - Y% pattern
                        {
                            // Exclude already handled patterns like part titles
                            if (!Regex.IsMatch(text, @"^סעיף\s([א-ת])\s*–\s*(\d+)%") &&
                                !Regex.IsMatch(text, @"^שאלות\s*(\d+)\s*/\s*(\d+).*"))
                            {
                                var stepMatch = Regex.Match(text, @"^(.+?)\s*–\s*(\d+)%");
                                if (stepMatch.Success)
                                {
                                    var step = new SolutionStep
                                    {
                                        Description = stepMatch.Groups[1].Value.Trim(),
                                        Value = int.Parse(stepMatch.Groups[2].Value),
                                        Type = "Score"
                                    };

                                    if (currentPart != null)
                                    {
                                        currentPart.SolutionSteps.Add(step);
                                    }
                                    else if (currentQuestion.Parts.Count == 0 || currentQuestion.GeneralSolutionSteps.Any())
                                    {
                                        currentQuestion.GeneralSolutionSteps.Add(step);
                                    }
                                    continue; // Processed, move to next paragraph
                                }
                            }
                        }
                    }
                }
            }
        }
        // Method to convert parsed data into a flat list of Metric entities
        public List<Metric> GetMetrics(int examId)
        {
            var metrics = new List<Metric>();

            // Add General Rules
            foreach (var rule in GeneralRules)
            {
                metrics.Add(new Metric
                {
                    ExamId = examId,
                    QuestionNumber = "0", // General rules don't belong to a specific question
                    Part = "General",    // Explicitly set "General" for clarity
                    RuleDescription = rule.Description,
                    Score = rule.Value > 0 ? (int?)rule.Value : null, // Use extracted value, null if 0
                    ScoreType = rule.Type, // Will be "GeneralPenalty"
                    Status = null        // Initially null, not yet modified by admin
                });
            }

            // Add Exam Questions and their parts/steps/reductions
            foreach (var question in ExamQuestions)
            {
                // Add the question title itself as a metric entry
                metrics.Add(new Metric
                {
                    ExamId = examId,
                    QuestionNumber = question.QuestionNumber,
                    Part = null,
                    RuleDescription = question.QuestionTitle,
                    Score = null, // Question titles don't have a score
                    ScoreType = "Question Title",
                    Status = null
                });

                // Handle general solution steps and reductions for the question (if no explicit parts)
                foreach (var step in question.GeneralSolutionSteps)
                {
                    metrics.Add(new Metric
                    {
                        ExamId = examId,
                        QuestionNumber = question.QuestionNumber,
                        Part = null, // No specific part
                        RuleDescription = step.Description,
                        Score = step.Value,
                        ScoreType = step.Type, // "Score"
                        Status = null
                    });
                }
                foreach (var alt in question.GeneralAlternativeSolutions)
                {
                    metrics.Add(new Metric
                    {
                        ExamId = examId,
                        QuestionNumber = question.QuestionNumber,
                        Part = null, // No specific part
                        RuleDescription = alt,
                        Score = null,
                        ScoreType = "Alternative Solution",
                        Status = null
                    });
                }
                foreach (var reduction in question.GeneralReductions)
                {
                    metrics.Add(new Metric
                    {
                        ExamId = examId,
                        QuestionNumber = question.QuestionNumber,
                        Part = null, // No specific part
                        RuleDescription = reduction.Mistake,
                        Score = reduction.Value,
                        ScoreType = reduction.Type, // "Penalty"
                        Status = null
                    });
                }


                if (question.Parts.Any())
                {
                    foreach (var part in question.Parts)
                    {
                        // Add the part's total score as a specific entry
                        metrics.Add(new Metric
                        {
                            ExamId = examId,
                            QuestionNumber = question.QuestionNumber,
                            Part = part.PartName,
                            RuleDescription = $"ציון לסעיף {part.PartName}",
                            Score = part.PartScore,
                            ScoreType = "Part Score", // Changed from "Score" for clarity
                            Status = null
                        });

                        foreach (var step in part.SolutionSteps)
                        {
                            metrics.Add(new Metric
                            {
                                ExamId = examId,
                                QuestionNumber = question.QuestionNumber,
                                Part = part.PartName,
                                RuleDescription = step.Description,
                                Score = step.Value,
                                ScoreType = step.Type, // "Score"
                                Status = null
                            });
                        }
                        foreach (var alt in part.AlternativeSolutions)
                        {
                            metrics.Add(new Metric
                            {
                                ExamId = examId,
                                QuestionNumber = question.QuestionNumber,
                                Part = part.PartName,
                                RuleDescription = alt,
                                Score = null,
                                ScoreType = "Alternative Solution",
                                Status = null
                            });
                        }
                        foreach (var reduction in part.Reductions)
                        {
                            metrics.Add(new Metric
                            {
                                ExamId = examId,
                                QuestionNumber = question.QuestionNumber,
                                Part = part.PartName,
                                RuleDescription = reduction.Mistake,
                                Score = reduction.Value,
                                ScoreType = reduction.Type, // "Penalty"
                                Status = null
                            });
                        }
                    }
                }
            }
            return metrics;
        }
    }
}