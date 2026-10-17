using System;
using System.Collections.Generic;
using NextXI.Core.Data;
using NextXI.Core.World;

namespace NextXI.Core.Career
{
    public enum OfferType { Transfer, Loan, Renewal, FreeAgent }
    public enum OfferStatus { Pending, Accepted, Rejected, Withdrawn, Expired, Countered }

    [Serializable]
    public sealed class TransferOffer
    {
        public string Id;
        public OfferType Type;
        public string ClubId;
        public int Wage;
        public int Years;
        public SquadRole Role;
        public long Fee;
        public int ExpiresDay;
        public OfferStatus Status = OfferStatus.Pending;
        public int Counters;
        public string Note;
    }

    [Serializable]
    public sealed class Objective
    {
        public string Id;
        public string Text;
        /// <summary>goals, assists, apps, rating, cleansheets, starts</summary>
        public string Metric;
        public float Target;
        public float Progress;
        /// <summary>Last day (season day) to complete it.</summary>
        public int Deadline;
        public int RewardTrust = 5;
        public int RewardXp = 60;
        public bool Done, Failed;
        public bool SeasonLong;
    }

    public enum NewsCategory { Result, Player, Transfer, Rumour, Injury, Coach, Record, NationalTeam, Award, Club, Interview }

    [Serializable]
    public sealed class NewsItem
    {
        public string Id;
        public int Season, Day;
        public NewsCategory Category;
        public string Headline;
        public string Body;
        public string ClubId;
        public string PlayerId;
        /// <summary>1 = minor ... 5 = front page.</summary>
        public int Importance = 2;
        public string TemplateId;
    }

    [Serializable]
    public sealed class SocialPost
    {
        public int Season, Day;
        public string Author;
        public string Handle;
        public string Text;
        public int Likes;
        /// <summary>-1 negative, 0 neutral, 1 positive (towards the athlete).</summary>
        public int Sentiment;
    }

    [Serializable]
    public sealed class InboxMessage
    {
        public string Id;
        public int Season, Day;
        public string From;
        public string Title;
        public string Body;
        public bool Read;
        /// <summary>Optional link to an offer id.</summary>
        public string OfferId;
    }

    [Serializable]
    public sealed class AthleteMatchLog
    {
        public int Season, Day;
        public string CompetitionId;
        public string OpponentId;
        public bool Home;
        public int GoalsFor, GoalsAgainst;
        public int Minutes, Goals, Assists;
        public float Rating;
        public bool Started;
        public string Level;
    }

    [Serializable]
    public sealed class TrainingResult
    {
        public int Season, Day;
        public string DrillId;
        public float Score;
        public int Xp;
    }

    [Serializable]
    public sealed class Award
    {
        public int Season;
        public string Name;
        public string Detail;
    }

    /// <summary>Complete persistent state of one player career (one save slot).</summary>
    [Serializable]
    public sealed class CareerState
    {
        public const int FormatVersion = 1;

        public string Id;
        public string SaveName;
        public string CreatedUtc;
        public string LastSavedUtc;
        public ulong Seed;

        public string AthleteId;
        public string ArchetypeId;
        public CareerOrigin Origin;
        public int Level = 1;
        public int Xp;
        public int XpThisWeek;
        public List<string> UnlockedPerks = new List<string>();
        public List<string> PendingPerkChoice = new List<string>();
        public Dictionary<string, float> AttributeProgress = new Dictionary<string, float>();

        public WorldState World;
        /// <summary>Days since the start of the current season.</summary>
        public int Day;
        public bool InYouthTeam;

        // Coach relationship
        public float Trust = 50f;
        public string CoachId;
        public Position PreferredPosition;
        public bool StartRequestPending;
        public int LastCoachTalkDay = -100;

        public List<Objective> Objectives = new List<Objective>();
        public List<TransferOffer> Offers = new List<TransferOffer>();
        public List<NewsItem> News = new List<NewsItem>();
        public List<SocialPost> Social = new List<SocialPost>();
        public List<InboxMessage> Inbox = new List<InboxMessage>();
        public List<AthleteMatchLog> MatchLog = new List<AthleteMatchLog>();
        public List<TrainingResult> Training = new List<TrainingResult>();
        public int TrainingSessionsThisWeek;
        public List<Award> Awards = new List<Award>();
        public List<string> Titles = new List<string>();
        public string AgentName = "Rafa Moreno (agente)";
        public int NationalCaps, NationalGoals;
        public bool Retired;
        public int RetiredSeason;
        public int NextId = 1;
        public List<string> RecentTemplates = new List<string>();

        public PlayerProfile Athlete => World?.Player(AthleteId);
        public PlayerState AthleteState => World?.State(AthleteId);
        public string ClubId => AthleteState?.Contract.ClubId;
        public ClubRecord Club => World?.Club(ClubId);
        public Archetype Archetype => Archetype.Get(ArchetypeId);

        public string NewId(string prefix) => $"{prefix}{NextId++}";

        public DateTime Date => WorldBuilder.SeasonStart(World).AddDays(Day);
    }
}
