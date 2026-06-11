using backend.Data.Entities;
using backend.Models;

namespace backend.Data;

/// <summary>
/// Seeds the database with initial development data if the Users table is empty.
/// </summary>
public static class DataSeeder
{
    public static void Seed(DecidirDbContext db)
    {
        if (db.Users.Any())
        {
            return; // Already seeded
        }

        var alex   = new UserEntity { Id = Guid.Parse("89f651a2-d6ad-43b6-a2d8-209da7599387"), UserName = "alex_t",   DisplayName = "Alex",   Role = UserRole.Member };
        var jordan = new UserEntity { Id = Guid.Parse("03a431ca-7354-43b8-b8f3-cf95f65f83b4"), UserName = "jordan_r", DisplayName = "Jordan", Role = UserRole.Member };
        var casey  = new UserEntity { Id = Guid.Parse("c421252a-2976-4f97-9fbf-e9f848f066f8"), UserName = "casey_l",  DisplayName = "Casey",  Role = UserRole.Member };
        var morgan = new UserEntity { Id = Guid.Parse("8af01b3a-d4b4-4954-9805-6dc58a2f0e0c"), UserName = "morgan_p", DisplayName = "Morgan", Role = UserRole.Member };
        var sam    = new UserEntity { Id = Guid.Parse("e1d2e6fb-c79f-4d18-8dd9-c9507487e2c4"), UserName = "sam_k",    DisplayName = "Sam",    Role = UserRole.Moderator };

        db.Users.AddRange(alex, jordan, casey, morgan, sam);

        var now = DateTime.UtcNow;

        var case1 = new CaseEntity
        {
            Id = Guid.Parse("2fd6fa9e-8ed5-4ea3-b0ef-e42fdf47c2f1"),
            Title = "Was cancelling 20 minutes before dinner justified?",
            Category = "Relationships",
            Summary = "One side says a last-minute work escalation made attendance impossible. The other says there was enough notice to communicate sooner.",
            SideAUserId = alex.Id,
            SideAUserName = alex.UserName,
            SideAClaim = "I had an urgent production incident and couldn't step away without risking customer downtime.",
            SideAPostedAtUtc = now.AddHours(-16),
            SideBUserId = jordan.Id,
            SideBUserName = jordan.UserName,
            SideBClaim = "I started cooking hours earlier and only learned the cancellation at the last moment.",
            SideBPostedAtUtc = now.AddHours(-15),
            Status = CaseStatus.Open,
            CreatedAtUtc = now.AddDays(-1)
        };

        var case2 = new CaseEntity
        {
            Id = Guid.Parse("af1ea95c-9f7f-4cd5-b948-3c2f12c31f74"),
            Title = "Should roommates split utility overuse charges?",
            Category = "Roommates",
            Summary = "Heating bill spiked. Side A wants a proportional split based on room heater usage. Side B wants an equal split.",
            SideAUserId = casey.Id,
            SideAUserName = casey.UserName,
            SideAClaim = "The meter smart plugs show one room used triple the power, so the extra should not be shared equally.",
            SideAPostedAtUtc = now.AddHours(-30),
            SideBUserId = morgan.Id,
            SideBUserName = morgan.UserName,
            SideBClaim = "We agreed to split utilities evenly from day one, and changing that retroactively is unfair.",
            SideBPostedAtUtc = now.AddHours(-28),
            Status = CaseStatus.Open,
            CreatedAtUtc = now.AddDays(-2)
        };

        db.Cases.AddRange(case1, case2);

        db.CaseComments.AddRange(
            new CaseCommentEntity { Id = Guid.NewGuid(), CaseId = case1.Id, UserId = casey.Id, UserName = casey.UserName, Message = "Both sides have a point, but timing and communication matter most here.", CreatedAtUtc = now.AddHours(-10) },
            new CaseCommentEntity { Id = Guid.NewGuid(), CaseId = case1.Id, UserId = morgan.Id, UserName = morgan.UserName, Message = "If it was truly urgent, a quick heads-up earlier would have helped.", CreatedAtUtc = now.AddHours(-9) },
            new CaseCommentEntity { Id = Guid.NewGuid(), CaseId = case2.Id, UserId = alex.Id, UserName = alex.UserName, Message = "Smart plug data feels like fair evidence for a proportional split.", CreatedAtUtc = now.AddHours(-20) }
        );

        db.CaseEvidence.AddRange(
            new CaseEvidenceEntity
            {
                Id = Guid.NewGuid(),
                CaseId = case1.Id,
                Side = CaseSide.A,
                AddedByUserId = alex.Id,
                AddedByUserName = alex.UserName,
                Type = CaseEvidenceType.Link,
                Title = "Incident postmortem timeline",
                ResourceUrl = "https://example.com/postmortem-timeline",
                MimeType = "",
                SizeBytes = 0,
                CreatedAtUtc = now.AddHours(-14)
            },
            new CaseEvidenceEntity
            {
                Id = Guid.NewGuid(),
                CaseId = case1.Id,
                Side = CaseSide.B,
                AddedByUserId = jordan.Id,
                AddedByUserName = jordan.UserName,
                Type = CaseEvidenceType.Link,
                Title = "Dinner prep receipts and timeline",
                ResourceUrl = "https://example.com/dinner-receipts",
                MimeType = "",
                SizeBytes = 0,
                CreatedAtUtc = now.AddHours(-13)
            },
            new CaseEvidenceEntity
            {
                Id = Guid.NewGuid(),
                CaseId = case2.Id,
                Side = CaseSide.A,
                AddedByUserId = casey.Id,
                AddedByUserName = casey.UserName,
                Type = CaseEvidenceType.Link,
                Title = "Smart plug usage report",
                ResourceUrl = "https://example.com/smart-plug-report",
                MimeType = "",
                SizeBytes = 0,
                CreatedAtUtc = now.AddHours(-27)
            }
        );

        db.UserRewards.AddRange(
            new UserRewardEntity { Id = Guid.NewGuid(), UserId = alex.Id,   BadgeCode = "POST_PARTICIPATION", SourceType = "CaseCreate", SourceId = case1.Id, Reason = "Posted the Side A argument in a new case.", AwardedAtUtc = now },
            new UserRewardEntity { Id = Guid.NewGuid(), UserId = jordan.Id, BadgeCode = "POST_PARTICIPATION", SourceType = "CaseCreate", SourceId = case1.Id, Reason = "Posted the Side B argument in a new case.", AwardedAtUtc = now },
            new UserRewardEntity { Id = Guid.NewGuid(), UserId = casey.Id,  BadgeCode = "POST_PARTICIPATION", SourceType = "CaseCreate", SourceId = case2.Id, Reason = "Posted the Side A argument in a new case.", AwardedAtUtc = now },
            new UserRewardEntity { Id = Guid.NewGuid(), UserId = morgan.Id, BadgeCode = "POST_PARTICIPATION", SourceType = "CaseCreate", SourceId = case2.Id, Reason = "Posted the Side B argument in a new case.", AwardedAtUtc = now }
        );

        db.SaveChanges();
    }
}
