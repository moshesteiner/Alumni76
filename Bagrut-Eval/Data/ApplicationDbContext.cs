// Data/ApplicationDbContext.cs
using Bagrut_Eval.Models; // Your Models namespace
using Microsoft.EntityFrameworkCore;

namespace Bagrut_Eval.Data // Your DbContext namespace
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        // Existing DbSets
        public DbSet<User> Users { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Issue> Issues { get; set; }
        public DbSet<Part> Parts { get; set; }
        public DbSet<AllowedExam> AllowedExams { get; set; }
        public DbSet<IssueLog> IssueLogs { get; set; }
        public DbSet<ExamLog> ExamsLog { get; set; }
        public DbSet<UserLog> UsersLog { get; set; }

        // NEW DbSets for refactored models
        public DbSet<Drawing> Drawings { get; set; }
        public DbSet<Answer> Answers { get; set; }
        public DbSet<Export> Exports { get; set; }
        public DbSet<LastLogin> LastLogins { get; set; }

        // While logic of Metrics REMOVED from the current solution, I leave the DbSets for potential future use
        public DbSet<Metric> Metrics { get; set; }
        public DbSet<MetricLog> MetricsLog { get; set; }
        public DbSet<Subject> Subjects { get; set; } = null!;
        public DbSet<UserSubject> UserSubjects { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- USER Configurations ---
            modelBuilder.Entity<Issue>()
               .Property(i => i.Status)
               .HasConversion(new IssueStatusConverter());// This tells EF Core:
                                                          // - When saving: Convert the IssueStatus enum to its string name (e.g., "Open", "InProgress")
                                                          // - When loading: Convert the string name from the DB back to the IssueStatus enum.

            // Ensure Email is unique
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // --- ISSUE Configurations ---
            // Relationship: Issue -> User (Initiator)
            modelBuilder.Entity<Issue>()
                .HasOne(i => i.User)
                .WithMany(u => u.CreatedIssues)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete on User deletion

            // NEW: Relationship: Issue -> Answer (FinalAnswer) - One-to-one (or one-to-zero/one)
            modelBuilder.Entity<Issue>()
                .HasOne(i => i.FinalAnswer) // An Issue has one optional FinalAnswer
                .WithOne() // A FinalAnswer can be the FinalAnswer for only one Issue
                .HasForeignKey<Issue>(i => i.FinalAnswerId) // Issue entity holds the foreign key
                .IsRequired(false) // AnswerId is nullable
                .OnDelete(DeleteBehavior.Restrict); // Important: Consider cascade/restrict based on your needs

            // NEW: Relationship: Issue -> Drawings (One-to-many)
            modelBuilder.Entity<Issue>()
                .HasMany(i => i.Drawings)
                .WithOne(d => d.Issue)
                .HasForeignKey(d => d.IssueId)
                .OnDelete(DeleteBehavior.Cascade); // If Issue is deleted, its Drawings should be deleted

            // NEW: Relationship: Issue -> Answers (Discussion - One-to-many)
            modelBuilder.Entity<Issue>()
                .HasMany(i => i.Answers)
                .WithOne(a => a.Issue)
                .HasForeignKey(a => a.IssueId)
                .OnDelete(DeleteBehavior.Cascade); // If Issue is deleted, its discussion Answers should be deleted       

            // --- PART Configurations ---
            // Relationship: Issue -> Part (Many-to-one)
            modelBuilder.Entity<Issue>()
                .HasOne(i => i.Part)
                .WithMany(p => p.Issues)
                .HasForeignKey(i => i.PartId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship: Part -> Exam (Many-to-one)
            modelBuilder.Entity<Part>()
                .HasOne(p => p.Exam)
                .WithMany(e => e.Parts)
                .HasForeignKey(p => p.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- ALLOWED EXAM Configurations ---
            // Configure the composite primary key for AllowedExam
            modelBuilder.Entity<AllowedExam>()
                .HasKey(ae => new { ae.UserId, ae.ExamId });

            // Configure the relationships for AllowedExam
            modelBuilder.Entity<AllowedExam>()
                .HasOne(ae => ae.User)
                .WithMany(u => u.AllowedExams)
                .HasForeignKey(ae => ae.UserId);

            modelBuilder.Entity<AllowedExam>()
                .HasOne(ae => ae.Exam)
                .WithMany(e => e.AllowedExams)
                .HasForeignKey(ae => ae.ExamId);

            // --- ISSUE LOG Configurations ---
            modelBuilder.Entity<IssueLog>()
                .HasOne(il => il.Issue)
                .WithMany(i => i.IssueLogs) // Updated to use IssueLogs collection on Issue
                .HasForeignKey(il => il.IssueId)
                .OnDelete(DeleteBehavior.Cascade); // If an Issue is deleted, its logs should be deleted

            modelBuilder.Entity<IssueLog>()
                .HasOne(il => il.User) // The user who created this log entry
                .WithMany(u => u.CreatedIssueLogs) // Updated to use CreatedIssueLogs collection on User
                .HasForeignKey(il => il.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Do not delete user if their log entries are deleted

            // --- EXAM LOG Configurations ---
            modelBuilder.Entity<ExamLog>()
                .HasOne(el => el.Exam)
                .WithMany(e => e.ExamLogs) // Assuming ExamLogs is the collection on Exam
                .HasForeignKey(el => el.ExamId)
                .OnDelete(DeleteBehavior.Cascade); // If Exam is deleted, its logs should be deleted

            modelBuilder.Entity<ExamLog>()
                .HasOne(el => el.User)
                .WithMany(u => u.ExamLogs) // Assuming ExamLogs is the collection on User (for logs created by user)
                .HasForeignKey(el => el.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Do not delete user if their exam log entries are deleted

            // --- USER LOG Configurations ---
            // Relationship: UserLog -> User (Subject of the log)
            modelBuilder.Entity<UserLog>()
                .HasOne(ul => ul.User) // The user being logged about
                .WithMany(u => u.UserLogsAsSubject) // User model needs this collection
                .HasForeignKey(ul => ul.UserId)
                .OnDelete(DeleteBehavior.Cascade); // If user is deleted, their logs are deleted

            // Relationship: UserLog -> User (Initiator of the log)
            modelBuilder.Entity<UserLog>()
                .HasOne(ul => ul.Initiator) // The user who initiated the log
                .WithMany(u => u.CreatedUserLogs) // User model needs this collection
                .HasForeignKey(ul => ul.InitiatorId)
                .IsRequired(false) // InitiatorId is nullable
                .OnDelete(DeleteBehavior.Restrict); // Don't delete initiator if their logs are deleted

            // Relationship: Answer -> User (Senior who provided the answer)
            modelBuilder.Entity<Answer>()
                .HasOne(a => a.Senior)
                .WithMany(u => u.AnswersGiven) // User model needs this collection
                .HasForeignKey(a => a.SeniorId)
                .OnDelete(DeleteBehavior.Restrict); // Don't delete user if their answers are deleted

            // Export
            modelBuilder.Entity<Export>()
                .HasKey(e => e.IssueId); // Explicitly define IssueId as the primary key

            modelBuilder.Entity<Export>()
                .HasOne(e => e.Issue) // An Export has one Issue
                .WithOne(i => i.Export) // An Issue has one Export
                .HasForeignKey<Export>(e => e.IssueId); // Use IssueId as the foreign key

            // Relationship: Export -> User (Senior who performed the export)
            modelBuilder.Entity<Export>()
                .HasOne(e => e.Senior)
                .WithMany(u => u.ExportsPerformed) // User model needs this collection
                .HasForeignKey(e => e.SeniorId)
                .OnDelete(DeleteBehavior.Restrict); // Don't delete user if their exports are deleted

            // Relationship: LastLogin -> User
            modelBuilder.Entity<LastLogin>()
                .HasOne(ll => ll.User)
                .WithMany(u => u.LoginRecords) // User model needs this collection
                .HasForeignKey(ll => ll.UserId)
                .OnDelete(DeleteBehavior.Cascade); // If user is deleted, their login records are deleted

            // REMOVED: Metric and MetricLog configurations
            // No longer needed: modelBuilder.Entity<Exam>().HasMany(e => e.Metrics)...

            // Metric entity configuration
            modelBuilder.Entity<Metric>(entity =>
            {
                // Table name is already set by [Table("metrics")] in the model
                // Primary Key and Identity are also handled by Data Annotations ([Key], [DatabaseGenerated])

                // Configure the relationship with Exam
                entity.HasOne(m => m.Exam)           // A Metric has one Exam
                      .WithMany()                    // An Exam can have many Metrics (assuming no navigation property back in Exam)
                      .HasForeignKey(m => m.ExamId)  // The foreign key is ExamId in Metric
                      .IsRequired(false)             // ExamId is not marked [Required], so it can be null if needed
                      .OnDelete(DeleteBehavior.Restrict); // Prevent cascading delete of Exam if Metrics exist
            });

            // MetricLog entity configuration
            modelBuilder.Entity<MetricLog>(entity =>
            {
                // Table name is already set by [Table("MetricsLog")] in the model
                // Primary Key and Identity are also handled by Data Annotations ([Key], [DatabaseGenerated])

                // Configure the relationship with Metric
                entity.HasOne<Metric>()              // A MetricLog belongs to one Metric
                      .WithMany()                    // A Metric can have many MetricLogs (assuming no navigation property back in Metric)
                      .HasForeignKey(ml => ml.MetricId) // The foreign key is MetricId in MetricLog
                      .OnDelete(DeleteBehavior.Cascade); // If a Metric is deleted, its logs are also deleted.
                                                         // Consider DeleteBehavior.Restrict if you want to keep logs even if the Metric is gone.

                // Configure the relationship with User
                entity.HasOne<User>()                // A MetricLog has one User (the initiator)
                         .WithMany()                    // A User can have many MetricLogs (assuming no navigation property back in User)
                         .HasForeignKey(ml => ml.UserId) // The foreign key is UserId in MetricLog
                         .OnDelete(DeleteBehavior.Restrict); // Prevent cascading delete of a User if MetricLogs exist.

                // Configure properties (mostly handled by Data Annotations, but explicit can be here)
                // entity.Property(ml => ml.Action).IsRequired().HasMaxLength(50);
                // entity.Property(ml => ml.RuleDescription).IsRequired(); // Assuming this should match Metric's [Required] status
                // entity.Property(ml => ml.ScoreType).IsRequired(); // Assuming this should match Metric's [Required] status,
                // though Metric's ScoreType is nullable.
                // Ensure consistency if needed: MetricLog's ScoreType is not nullable.
            });


            modelBuilder.Entity<UserSubject>()
                            .HasKey(us => new { us.UserId, us.SubjectId }); // Define composite primary key

            modelBuilder.Entity<UserSubject>()
                .HasOne(us => us.User)
                .WithMany(u => u.UserSubjects)
                .HasForeignKey(us => us.UserId);

            modelBuilder.Entity<UserSubject>()
                .HasOne(us => us.Subject)
                .WithMany(s => s.UserSubjects)
                .HasForeignKey(us => us.SubjectId)
                .OnDelete(DeleteBehavior.Cascade); // Adjust DeleteBehavior as needed
        }
    }
}